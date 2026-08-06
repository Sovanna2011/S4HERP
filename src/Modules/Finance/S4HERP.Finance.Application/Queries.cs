using Microsoft.EntityFrameworkCore;
using S4HERP.BuildingBlocks.Application;
using S4HERP.BuildingBlocks.Infrastructure;
using S4HERP.Finance.Domain;
using S4HERP.Organization.Domain;

namespace S4HERP.Finance.Application;

// ----------------------------------------------------------- trial balance

[RequiresAuthorization("F_BKPF_BUK", "03")]
public sealed record TrialBalanceQuery : IQuery<TrialBalanceResult>
{
    public required string CompanyCode { get; init; }
    public required short FiscalYear { get; init; }
    public byte? FromPeriod { get; init; }
    public byte? ToPeriod { get; init; }
}

public sealed record TrialBalanceRow(
    string GLAccount, string Description, decimal Debit, decimal Credit, decimal Balance);

public sealed record TrialBalanceResult
{
    public required string CompanyCode { get; init; }
    public required short FiscalYear { get; init; }
    public required string Currency { get; init; }
    public required IReadOnlyList<TrialBalanceRow> Rows { get; init; }
    public required decimal TotalDebit { get; init; }
    public required decimal TotalCredit { get; init; }

    /// <summary>
    /// Must be zero. A non-zero total on a trial balance derived from the journal
    /// would mean the journal itself is unbalanced.
    /// </summary>
    public required decimal Difference { get; init; }
}

public sealed class TrialBalanceQueryHandler(
    S4herpDbContext db, IAuthorizationEnforcer authorization)
    : IQueryHandler<TrialBalanceQuery, TrialBalanceResult>
{
    public async Task<TrialBalanceResult> HandleAsync(
        TrialBalanceQuery query, CancellationToken cancellationToken)
    {
        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", query.CompanyCode), ("ACTVT", "03")], cancellationToken);

        var companyCode = await db.Set<CompanyCode>()
            .SingleOrDefaultAsync(c => c.Code == query.CompanyCode, cancellationToken)
            ?? throw new NotFoundException($"Company code {query.CompanyCode} does not exist.");

        var currency = await db.Set<Currency>()
            .Where(c => c.Id == companyCode.LocalCurrencyId)
            .Select(c => c.Code)
            .SingleAsync(cancellationToken);

        var fromPeriod = query.FromPeriod ?? 1;
        var toPeriod = query.ToPeriod ?? 16;

        // Derived from the journal rather than from a maintained balance table,
        // so "subledger reconciles to G/L" is an invariant instead of a nightly job.
        var rows = await (
            from line in db.Set<JournalEntryLine>().AsNoTracking()
            join header in db.Set<JournalEntryHeader>()
                on new { line.TenantId, line.CompanyCodeId, line.FiscalYear, line.DocumentNumber }
                equals new { header.TenantId, header.CompanyCodeId, header.FiscalYear, header.DocumentNumber }
            join account in db.Set<GLAccount>() on line.GLAccountId equals account.Id
            where line.CompanyCodeId == companyCode.Id
                  && line.FiscalYear == query.FiscalYear
                  && header.FiscalPeriod >= fromPeriod && header.FiscalPeriod <= toPeriod
                  && header.Status == JournalStatus.Posted
            group line by new { account.AccountNumber, account.LongText } into g
            orderby g.Key.AccountNumber
            select new
            {
                g.Key.AccountNumber,
                g.Key.LongText,
                Debit = g.Where(l => l.LocalAmount > 0).Sum(l => (decimal?)l.LocalAmount) ?? 0m,
                Credit = -(g.Where(l => l.LocalAmount < 0).Sum(l => (decimal?)l.LocalAmount) ?? 0m),
            }).ToListAsync(cancellationToken);

        var mapped = rows
            .Select(r => new TrialBalanceRow(
                r.AccountNumber, r.LongText, r.Debit, r.Credit, r.Debit - r.Credit))
            .ToList();

        var totalDebit = mapped.Sum(r => r.Debit);
        var totalCredit = mapped.Sum(r => r.Credit);

        return new TrialBalanceResult
        {
            CompanyCode = query.CompanyCode,
            FiscalYear = query.FiscalYear,
            Currency = currency,
            Rows = mapped,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            Difference = totalDebit - totalCredit,
        };
    }
}

// ------------------------------------------------------------ document read

[RequiresAuthorization("F_BKPF_BUK", "03")]
public sealed record GetJournalEntryQuery : IQuery<JournalEntryDocument>
{
    public required string CompanyCode { get; init; }
    public required short FiscalYear { get; init; }
    public required long DocumentNumber { get; init; }
}

public sealed record JournalEntryDocument
{
    public required string CompanyCode { get; init; }
    public required short FiscalYear { get; init; }
    public required long DocumentNumber { get; init; }
    public required string DocumentNumberFormatted { get; init; }
    public required string DocumentType { get; init; }
    public required DateOnly PostingDate { get; init; }
    public required byte FiscalPeriod { get; init; }
    public required string Status { get; init; }
    public required string Currency { get; init; }
    public string? Reference { get; init; }
    public string? HeaderText { get; init; }
    public required IReadOnlyList<JournalEntryDocumentLine> Lines { get; init; }
}

public sealed record JournalEntryDocumentLine(
    short LineNumber, string PostingKey, string DebitCredit, string AccountType,
    string? GLAccount, string? BusinessPartner, string? CostCenter, string? ProfitCenter,
    decimal DocumentAmount, decimal LocalAmount, string? LineText);

public sealed class GetJournalEntryQueryHandler(
    S4herpDbContext db, IAuthorizationEnforcer authorization)
    : IQueryHandler<GetJournalEntryQuery, JournalEntryDocument>
{
    public async Task<JournalEntryDocument> HandleAsync(
        GetJournalEntryQuery query, CancellationToken cancellationToken)
    {
        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", query.CompanyCode), ("ACTVT", "03")], cancellationToken);

        var companyCode = await db.Set<CompanyCode>()
            .SingleOrDefaultAsync(c => c.Code == query.CompanyCode, cancellationToken)
            ?? throw new NotFoundException($"Company code {query.CompanyCode} does not exist.");

        var header = await db.Set<JournalEntryHeader>().AsNoTracking()
            .Include(h => h.DocumentType)
            .SingleOrDefaultAsync(
                h => h.CompanyCodeId == companyCode.Id
                     && h.FiscalYear == query.FiscalYear
                     && h.DocumentNumber == query.DocumentNumber, cancellationToken)
            ?? throw new NotFoundException(
                $"Document {query.DocumentNumber} does not exist in company code " +
                $"{query.CompanyCode} for fiscal year {query.FiscalYear}.");

        var currency = await db.Set<Currency>()
            .Where(c => c.Id == header.DocumentCurrencyId).Select(c => c.Code)
            .SingleAsync(cancellationToken);

        var lines = await (
            from line in db.Set<JournalEntryLine>().AsNoTracking()
            where line.CompanyCodeId == companyCode.Id
                  && line.FiscalYear == query.FiscalYear
                  && line.DocumentNumber == query.DocumentNumber
            join account in db.Set<GLAccount>() on line.GLAccountId equals account.Id into accounts
            from account in accounts.DefaultIfEmpty()
            join partner in db.Set<BusinessPartner.Domain.Partner>()
                on line.BusinessPartnerId equals partner.Id into partners
            from partner in partners.DefaultIfEmpty()
            join costCenter in db.Set<Controlling.Domain.CostCenter>()
                on line.CostCenterId equals costCenter.Id into costCenters
            from costCenter in costCenters.DefaultIfEmpty()
            join profitCenter in db.Set<Controlling.Domain.ProfitCenter>()
                on line.ProfitCenterId equals profitCenter.Id into profitCenters
            from profitCenter in profitCenters.DefaultIfEmpty()
            orderby line.LineNumber
            select new JournalEntryDocumentLine(
                line.LineNumber,
                line.PostingKey,
                ((char)line.DebitCredit).ToString(),
                ((char)line.AccountType).ToString(),
                account.AccountNumber,
                partner.PartnerNumber,
                costCenter.Code,
                profitCenter.Code,
                line.DocumentAmount,
                line.LocalAmount,
                line.LineText)).ToListAsync(cancellationToken);

        return new JournalEntryDocument
        {
            CompanyCode = query.CompanyCode,
            FiscalYear = header.FiscalYear,
            DocumentNumber = header.DocumentNumber,
            DocumentNumberFormatted = header.DocumentNumberFormatted,
            DocumentType = header.DocumentType.Code,
            PostingDate = header.PostingDate,
            FiscalPeriod = header.FiscalPeriod,
            Status = header.Status.ToString(),
            Currency = currency,
            Reference = header.Reference,
            HeaderText = header.HeaderText,
            Lines = lines,
        };
    }
}
