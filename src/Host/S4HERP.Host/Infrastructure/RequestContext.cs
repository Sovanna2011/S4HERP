using Microsoft.EntityFrameworkCore;
using S4HERP.BuildingBlocks.Application;
using S4HERP.BuildingBlocks.Infrastructure;
using S4HERP.Security.Application;
using S4HERP.Security.Domain;

namespace S4HERP.Host.Infrastructure;

/// <summary>
/// Per-request identity, tenant and correlation. Populated by
/// <see cref="RequestContextMiddleware"/> before anything else runs.
/// </summary>
public sealed class RequestContext : ICurrentUser, ITenantContext, IUserContext, ICorrelationContext
{
    public long UserId { get; private set; }
    public string UserName { get; private set; } = "anonymous";
    public UserType UserType { get; private set; } = UserType.Dialog;
    public bool IsAuthenticated { get; private set; }

    public long TenantId { get; private set; }
    public bool IsCrossTenant => false;

    public Guid CorrelationId { get; private set; } = Guid.Empty;

    public void SetCorrelation(Guid correlationId) => CorrelationId = correlationId;

    public void SetUser(long tenantId, long userId, string userName, UserType userType)
    {
        TenantId = tenantId;
        UserId = userId;
        UserName = userName;
        UserType = userType;
        IsAuthenticated = true;
    }
}

public sealed class AuthenticationOptions
{
    /// <summary>
    /// "Development" trusts an <c>X-S4HERP-User</c> header. Anything else requires
    /// real authentication, which Phase 3 has not built yet.
    /// </summary>
    public string Mode { get; set; } = "None";
}

/// <summary>
/// Resolves the caller and the tenant for the request.
///
/// Development mode trusts a header naming the user. That is a real hole, so it
/// is gated three ways: the mode must be set explicitly, the environment must be
/// Development, and the host refuses to start if the two disagree (see
/// <see cref="ModuleRegistration"/>). It is replaced by OIDC/JWT before anything
/// leaves a developer machine.
/// </summary>
public sealed class RequestContextMiddleware(RequestDelegate next, AuthenticationOptions options)
{
    private const string UserHeader = "X-S4HERP-User";
    private const string CorrelationHeader = "X-Correlation-Id";

    public async Task InvokeAsync(
        HttpContext http, RequestContext context, SystemDbContextFactory systemContexts)
    {
        context.SetCorrelation(
            Guid.TryParse(http.Request.Headers[CorrelationHeader], out var supplied)
                ? supplied
                : Guid.NewGuid());
        http.Response.Headers[CorrelationHeader] = context.CorrelationId.ToString();

        if (options.Mode == "Development"
            && http.Request.Headers.TryGetValue(UserHeader, out var userName)
            && !string.IsNullOrWhiteSpace(userName))
        {
            // Cross-tenant by construction: the tenant is not known until the
            // user is, so this cannot run on the request-scoped context.
            await using var db = systemContexts.Create();
            var user = await db.Set<User>()
                .Where(u => u.UserName == userName.ToString() && u.IsActive)
                .Select(u => new { u.Id, u.TenantId, u.UserName, u.UserType })
                .SingleOrDefaultAsync(http.RequestAborted);

            if (user is not null)
            {
                context.SetUser(user.TenantId, user.Id, user.UserName, user.UserType);
            }
        }

        await next(http);
    }
}
