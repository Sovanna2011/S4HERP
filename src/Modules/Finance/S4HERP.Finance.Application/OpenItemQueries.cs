using Microsoft.EntityFrameworkCore;
using S4HERP.BuildingBlocks.Application;
using S4HERP.BuildingBlocks.Infrastructure;
using S4HERP.Finance.Domain;
using S4HERP.Organization.Domain;

namespace S4HERP.Finance.Application;

/// <summary>
/// FBL5N / FBL1N: what a partner still owes, or is still owed, and how late it
/// is. Derived from <c>fin.OpenItem</c>, which the posting engine maintains — not
/// recomputed from the journal, because clearing state is deliberately not in
/// the journal (ADR-09).
/// </summary>
[RequiresAuthorization("F_BKPF_BUK", "03")]
public sealed record OpenItemsQuery : IQuery<OpenItemsResult>
{
    public required string CompanyCode { get; init; }

    /// <summary>"D" for customers, "K" for vendors. Omitted returns both.</summary>
    public string? AccountType { get; init; }

    public string? BusinessPartner { get; init; }

    /// <summary>The date arrears are measured from. Defaults to today.</summary>
    public DateOnly? AsOf { get; init; }

    /// <summary>Include items already settled, for a full account statement.</summary>
    public bool IncludeCleared { get; init; }
}

public sealed record OpenItemRow(
    short FiscalYear,
    long DocumentNumber,
    short LineNumber,
    string DocumentNumberFormatted,
    string AccountType,
    string? BusinessPartner,
    string? BusinessPartnerName,
    DateOnly PostingDate,
    DateOnly? DueDate,
    int? DaysOverdue,
    string AgingBucket,
    decimal OriginalAmount,
    decimal OpenAmount,
    string Currency,
    string ClearingStatus,
    string? PaymentTerms,
    decimal? CashDiscountPercent,
    decimal? CashDiscountAmount);

public sealed record AgingBucketTotal(string Bucket, int Count, decimal OpenAmount);

public sealed record OpenItemsResult
{
    public required string CompanyCode { get; init; }
    public required DateOnly AsOf { get; init; }
    public required IReadOnlyList<OpenItemRow> Rows { get; init; }
    public required IReadOnlyList<AgingBucketTotal> Aging { get; init; }
    public required decimal TotalOpen { get; init; }
    public required decimal TotalOverdue { get; init; }
}

public sealed class OpenItemsQueryHandler(
    S4herpDbContext db, IAuthorizationEnforcer authorization, IClock clock)
    : IQueryHandler<OpenItemsQuery, OpenItemsResult>
{
    /// <summary>
    /// Upper bound of each bucket in days overdue. The last is open-ended. Kept
    /// as data rather than a chain of ifs because a customer will want their own
    /// bands, and this is the one place to change.
    /// </summary>
    private static readonly (string Name, int? UpperBound)[] Buckets =
    [
        ("Not due", 0),
        ("1-30", 30),
        ("31-60", 60),
        ("61-90", 90),
        ("90+", null),
    ];

    public async Task<OpenItemsResult> HandleAsync(
        OpenItemsQuery query, CancellationToken cancellationToken)
    {
        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", query.CompanyCode), ("ACTVT", "03")], cancellationToken);

        var companyCode = await db.Set<CompanyCode>()
            .SingleOrDefaultAsync(c => c.Code == query.CompanyCode, cancellationToken)
            ?? throw new NotFoundException($"Company code {query.CompanyCode} does not exist.");

        var asOf = query.AsOf ?? DateOnly.FromDateTime(clock.UtcNow);

        var accountType = query.AccountType switch
        {
            null or "" => (AccountType?)null,
            "D" => AccountType.Customer,
            "K" => AccountType.Vendor,
            _ => throw new BusinessRuleException(
                PostingErrors.UnknownObject,
                $"Account type {query.AccountType} is not a subledger type. Use D or K."),
        };

        var rows = await (
            from item in db.Set<OpenItem>().AsNoTracking()
            join header in db.Set<JournalEntryHeader>()
                on new { item.TenantId, item.CompanyCodeId, item.FiscalYear, item.DocumentNumber }
                equals new { header.TenantId, header.CompanyCodeId, header.FiscalYear, header.DocumentNumber }
            join partner in db.Set<BusinessPartner.Domain.Partner>()
                on item.BusinessPartnerId equals partner.Id into partners
            from partner in partners.DefaultIfEmpty()
            join currency in db.Set<Currency>() on item.DocumentCurrencyId equals currency.Id
            where item.CompanyCodeId == companyCode.Id
                  && (accountType == null || item.AccountType == accountType)
                  && (query.BusinessPartner == null
                      || partner.PartnerNumber == query.BusinessPartner)
                  && (query.IncludeCleared || item.ClearingStatus != ClearingStatus.Cleared)
            orderby item.DueDate, item.DocumentNumber, item.LineNumber
            select new
            {
                item.FiscalYear,
                item.DocumentNumber,
                item.LineNumber,
                header.DocumentNumberFormatted,
                header.PostingDate,
                header.DocumentDate,
                item.AccountType,
                PartnerNumber = (string?)partner.PartnerNumber,
                PartnerName = (string?)partner.Name,
                item.DueDate,
                item.OriginalAmountDocument,
                item.OpenAmountDocument,
                Currency = currency.Code,
                item.ClearingStatus,
                item.PaymentTerms,
            }).ToListAsync(cancellationToken);

        var termCodes = rows.Select(r => r.PaymentTerms).OfType<string>().Distinct().ToList();
        var terms = await db.Set<PaymentTerm>().AsNoTracking()
            .Where(t => termCodes.Contains(t.Code))
            .ToDictionaryAsync(t => t.Code, cancellationToken);

        var mapped = rows.Select(r =>
        {
            // Overdue only once the due date has passed; an item with no due date
            // has nothing to be late against and is reported as not due rather
            // than as maximally overdue.
            var daysOverdue = r.DueDate is { } due && asOf > due
                ? asOf.DayNumber - due.DayNumber
                : (int?)(r.DueDate is null ? null : 0);

            decimal? discountPercent = null;
            if (r.PaymentTerms is { } code && terms.TryGetValue(code, out var term))
            {
                discountPercent = term.CashDiscountPercentOn(
                    asOf, r.DocumentDate, r.PostingDate, r.PostingDate);
            }

            var open = Math.Abs(r.OpenAmountDocument);

            return new OpenItemRow(
                r.FiscalYear,
                r.DocumentNumber,
                r.LineNumber,
                r.DocumentNumberFormatted,
                ((char)r.AccountType).ToString(),
                r.PartnerNumber,
                r.PartnerName,
                r.PostingDate,
                r.DueDate,
                daysOverdue,
                BucketFor(daysOverdue),
                Math.Abs(r.OriginalAmountDocument),
                open,
                r.Currency,
                r.ClearingStatus.ToString(),
                r.PaymentTerms,
                discountPercent,
                discountPercent is { } p
                    ? Math.Round(open * p / 100m, 2, MidpointRounding.AwayFromZero)
                    : null);
        }).ToList();

        var aging = Buckets
            .Select(b => new AgingBucketTotal(
                b.Name,
                mapped.Count(r => r.AgingBucket == b.Name),
                mapped.Where(r => r.AgingBucket == b.Name).Sum(r => r.OpenAmount)))
            .ToList();

        return new OpenItemsResult
        {
            CompanyCode = companyCode.Code,
            AsOf = asOf,
            Rows = mapped,
            Aging = aging,
            TotalOpen = mapped.Sum(r => r.OpenAmount),
            TotalOverdue = mapped.Where(r => r.DaysOverdue > 0).Sum(r => r.OpenAmount),
        };
    }

    private static string BucketFor(int? daysOverdue)
    {
        if (daysOverdue is not { } days || days <= 0)
        {
            return Buckets[0].Name;
        }

        foreach (var (name, upper) in Buckets.Skip(1))
        {
            if (upper is null || days <= upper)
            {
                return name;
            }
        }

        return Buckets[^1].Name;
    }
}
