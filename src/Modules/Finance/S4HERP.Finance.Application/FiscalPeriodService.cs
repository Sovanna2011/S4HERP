using Microsoft.EntityFrameworkCore;
using S4HERP.BuildingBlocks.Application;
using S4HERP.BuildingBlocks.Infrastructure;
using S4HERP.Organization.Domain;

namespace S4HERP.Finance.Application;

public sealed record FiscalPeriodResult(short FiscalYear, byte Period);

public interface IFiscalPeriodService
{
    /// <summary>
    /// Derives (fiscal year, period) from a posting date. Never accepts a period
    /// supplied by the caller — accepting one is how backdated postings reach
    /// closed periods.
    /// </summary>
    Task<FiscalPeriodResult> DeriveAsync(
        long companyCodeId, DateOnly postingDate, CancellationToken cancellationToken);

    /// <summary>Throws when the period is closed for any of the account types present.</summary>
    Task RequireOpenAsync(
        long companyCodeId, FiscalPeriodResult period,
        IReadOnlyCollection<AccountType> accountTypes, CancellationToken cancellationToken);
}

public sealed class FiscalPeriodService(S4herpDbContext db) : IFiscalPeriodService
{
    public async Task<FiscalPeriodResult> DeriveAsync(
        long companyCodeId, DateOnly postingDate, CancellationToken cancellationToken)
    {
        var variantId = await db.Set<CompanyCode>()
            .Where(c => c.Id == companyCodeId)
            .Select(c => c.FiscalYearVariantId)
            .SingleAsync(cancellationToken);

        var period = await db.Set<FiscalYearPeriod>()
            .Where(p => p.FiscalYearVariantId == variantId
                        && !p.IsSpecialPeriod
                        && p.StartDate <= postingDate
                        && p.EndDate >= postingDate)
            .Select(p => new { p.FiscalYear, p.Period })
            .SingleOrDefaultAsync(cancellationToken);

        if (period is null)
        {
            throw new BusinessRuleException(
                PostingErrors.UnknownObject,
                $"No fiscal period is defined for posting date {postingDate:yyyy-MM-dd}. " +
                "The fiscal year calendar may not be maintained for that year.");
        }

        return new FiscalPeriodResult(period.FiscalYear, period.Period);
    }

    public async Task RequireOpenAsync(
        long companyCodeId, FiscalPeriodResult period,
        IReadOnlyCollection<AccountType> accountTypes, CancellationToken cancellationToken)
    {
        var variantId = await db.Set<CompanyCode>()
            .Where(c => c.Id == companyCodeId)
            .Select(c => c.PostingPeriodVariantId)
            .SingleAsync(cancellationToken);

        var states = await db.Set<PostingPeriod>()
            .Where(p => p.PostingPeriodVariantId == variantId
                        && p.FiscalYear == period.FiscalYear
                        && p.Period == period.Period
                        && accountTypes.Contains(p.AccountType))
            .Select(p => new { p.AccountType, p.IsOpen })
            .ToListAsync(cancellationToken);

        var violations = new List<RuleViolation>();

        foreach (var accountType in accountTypes)
        {
            var state = states.FirstOrDefault(s => s.AccountType == accountType);
            if (state is null || !state.IsOpen)
            {
                violations.Add(new RuleViolation(
                    "postingDate",
                    PostingErrors.PeriodClosed,
                    $"Period {period.Period:D2}/{period.FiscalYear} is closed for account type " +
                    $"{(char)accountType}."));
            }
        }

        if (violations.Count > 0)
        {
            throw new BusinessRuleException(
                PostingErrors.PeriodClosed,
                $"Posting period {period.Period:D2}/{period.FiscalYear} is not open.",
                violations);
        }
    }
}
