using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using S4HERP.BuildingBlocks.Application;
using S4HERP.BuildingBlocks.Infrastructure;
using S4HERP.Security.Domain;

namespace S4HERP.Security.Application;

/// <summary>Identity of the caller, resolved once per request.</summary>
public interface ICurrentUser
{
    long UserId { get; }
    string UserName { get; }
    UserType UserType { get; }
    bool IsAuthenticated { get; }
}

/// <summary>
/// Evaluates authorisation objects against the caller's roles.
///
/// Deny by default: no matching authorisation means no access. There is no
/// implicit grant and no "authenticated users may display".
/// </summary>
public sealed class AuthorizationEnforcer(
    S4herpDbContext db,
    ICurrentUser currentUser,
    ITenantContext tenant,
    IMemoryCache cache) : IAuthorizationEnforcer
{
    public async Task RequireAsync(
        string authorizationObject,
        IReadOnlyList<(string Field, string Value)> values,
        CancellationToken cancellationToken = default)
    {
        if (await IsAuthorizedAsync(authorizationObject, values, cancellationToken))
        {
            return;
        }

        var detail = values.Count == 0
            ? string.Empty
            : " for " + string.Join(", ", values.Select(v => $"{v.Field}={v.Value}"));

        // Name the missing authority: "nothing happens" is the worst error message
        // in an ERP, and knowing which object you lack is not a disclosure — the
        // caller already knows what they tried to do.
        throw new AuthorizationException(
            $"You do not hold authorisation object {authorizationObject}{detail}.");
    }

    public async Task<bool> IsAuthorizedAsync(
        string authorizationObject,
        IReadOnlyList<(string Field, string Value)> values,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated)
        {
            return false;
        }

        // An Auditor is display-only at the type level, not merely by role
        // assignment, so granting auditor access is a safe action.
        if (currentUser.UserType == UserType.Auditor
            && values.Any(v => v.Field == "ACTVT" && v.Value != "03"))
        {
            return false;
        }

        var grants = await LoadGrantsAsync(authorizationObject, cancellationToken);

        // A grant satisfies the request when every requested field is covered by
        // that same grant. Fields from different grants must not be combined —
        // "company code 1000" plus "activity 01" from two separate authorisations
        // does not authorise creating in 1000.
        return grants.Any(grant => values.All(requested =>
            grant.TryGetValue(requested.Field, out var permitted)
            && permitted.Any(p => p.Covers(requested.Value))));
    }

    public async Task<IReadOnlyCollection<long>> AuthorizedCompanyCodeIdsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated)
        {
            return [];
        }

        return await db.Set<UserCompanyCode>()
            .Where(x => x.UserId == currentUser.UserId)
            .Select(x => x.CompanyCodeId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Grants held by the caller for one authorisation object, as a list of
    /// field → permitted-value-range maps. Cached briefly: authorisation is read
    /// on every request, and role changes take effect within the window.
    /// </summary>
    private async Task<List<Dictionary<string, List<ValueRange>>>> LoadGrantsAsync(
        string authorizationObject, CancellationToken cancellationToken)
    {
        var key = $"authz:{tenant.TenantId}:{currentUser.UserId}:{authorizationObject}";

        if (cache.TryGetValue(key, out List<Dictionary<string, List<ValueRange>>>? cached)
            && cached is not null)
        {
            return cached;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var rows = await (
            from ur in db.Set<UserRole>()
            join ra in db.Set<RoleAuthorization>() on ur.RoleId equals ra.RoleId
            join ao in db.Set<AuthorizationObject>() on ra.AuthorizationObjectId equals ao.Id
            join rav in db.Set<RoleAuthorizationValue>() on ra.Id equals rav.RoleAuthorizationId
            join af in db.Set<AuthorizationField>() on rav.AuthorizationFieldId equals af.Id
            where ur.UserId == currentUser.UserId
                  && ur.ValidFrom <= today && ur.ValidTo > today
                  && ao.Code == authorizationObject
            select new
            {
                GrantId = ra.Id,
                Field = af.Code,
                rav.FromValue,
                rav.ToValue,
                rav.IsWildcard,
            }).ToListAsync(cancellationToken);

        var grants = rows
            .GroupBy(r => r.GrantId)
            .Select(g => g
                .GroupBy(r => r.Field)
                .ToDictionary(
                    f => f.Key,
                    f => f.Select(v => new ValueRange(v.FromValue, v.ToValue, v.IsWildcard)).ToList()))
            .ToList();

        cache.Set(key, grants, TimeSpan.FromSeconds(30));
        return grants;
    }

    private readonly record struct ValueRange(string From, string? To, bool IsWildcard)
    {
        public bool Covers(string value)
        {
            if (IsWildcard || From == "*")
            {
                return true;
            }

            return To is null
                ? string.Equals(From, value, StringComparison.OrdinalIgnoreCase)
                : string.CompareOrdinal(From, value) <= 0 && string.CompareOrdinal(value, To) <= 0;
        }
    }
}
