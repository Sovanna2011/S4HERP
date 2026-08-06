using Microsoft.EntityFrameworkCore;
using S4HERP.BuildingBlocks.Application;
using S4HERP.BuildingBlocks.Infrastructure;
using S4HERP.Finance.Domain;
using S4HERP.Organization.Domain;

namespace S4HERP.Finance.Application;

/// <summary>
/// FB08. Posts a mirror document rather than changing the original — the whole
/// point of an immutable ledger is that a correction is itself a fact.
/// </summary>
[RequiresAuthorization("F_BKPF_BUK", "01")]
public sealed record ReverseJournalEntryCommand : ICommand<ReverseJournalEntryResult>
{
    public required string CompanyCode { get; init; }
    public required short FiscalYear { get; init; }
    public required long DocumentNumber { get; init; }

    /// <summary>Mandatory and recorded: an unexplained reversal is an audit finding.</summary>
    public required string ReversalReasonCode { get; init; }

    /// <summary>
    /// Defaults to the original posting date. If that period is closed, the
    /// reversal moves to the current open period and says so.
    /// </summary>
    public DateOnly? PostingDate { get; init; }

    public string? IdempotencyKey { get; init; }
}

public sealed record ReverseJournalEntryResult
{
    public required string CompanyCode { get; init; }
    public required long ReversedDocumentNumber { get; init; }
    public required long ReversalDocumentNumber { get; init; }
    public required string ReversalDocumentNumberFormatted { get; init; }
    public required DateOnly ReversalPostingDate { get; init; }
    public required byte ReversalFiscalPeriod { get; init; }

    /// <summary>
    /// True when the original period was closed and the reversal was moved to an
    /// open one. Surfaced because it changes which period the accounts move in.
    /// </summary>
    public required bool PostingDateMoved { get; init; }

    public required int LinesReversed { get; init; }
    public required int OpenItemsCleared { get; init; }
}

public sealed class ReverseJournalEntryHandler(
    S4herpDbContext db,
    IFiscalPeriodService periods,
    INumberRangeAllocator numbers,
    IAuthorizationEnforcer authorization,
    IUserContext user,
    ITenantContext tenant,
    IClock clock,
    ICorrelationContext correlation)
    : ICommandHandler<ReverseJournalEntryCommand, ReverseJournalEntryResult>
{
    public async Task<ReverseJournalEntryResult> HandleAsync(
        ReverseJournalEntryCommand command, CancellationToken ct)
    {
        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", command.CompanyCode), ("ACTVT", "01")], ct);

        var companyCode = await db.Set<CompanyCode>()
            .SingleOrDefaultAsync(c => c.Code == command.CompanyCode, ct)
            ?? throw new NotFoundException($"Company code {command.CompanyCode} does not exist.");

        var original = await db.Set<JournalEntryHeader>()
            .Include(h => h.DocumentType)
            .SingleOrDefaultAsync(h => h.CompanyCodeId == companyCode.Id
                                       && h.FiscalYear == command.FiscalYear
                                       && h.DocumentNumber == command.DocumentNumber, ct)
            ?? throw new NotFoundException(
                $"Document {command.DocumentNumber} does not exist in {command.CompanyCode} " +
                $"for fiscal year {command.FiscalYear}.");

        Validate(original);

        var originalLines = await db.Set<JournalEntryLine>()
            .Where(l => l.CompanyCodeId == companyCode.Id
                        && l.FiscalYear == command.FiscalYear
                        && l.DocumentNumber == command.DocumentNumber)
            .OrderBy(l => l.LineNumber)
            .ToListAsync(ct);

        if (originalLines.Count == 0)
        {
            throw new BusinessRuleException(
                PostingErrors.UnknownObject, "The document has no lines to reverse.");
        }

        var (postingDate, period, moved) =
            await ResolveReversalPeriodAsync(command, companyCode, original, originalLines, ct);

        var reversalType = await ResolveReversalDocumentTypeAsync(original, ct);
        await authorization.RequireAsync(
            "F_BKPF_BLA", [("BLART", reversalType.Code), ("ACTVT", "01")], ct);

        var documentNumber = await numbers.AllocateAsync(
            NumberRangeObject.AccountingDocument, reversalType.NumberRangeCode,
            companyCode.Id, period.FiscalYear, ct);

        var formatted =
            $"KSS-{companyCode.Code}-{period.FiscalYear}-{reversalType.Code}-{documentNumber:D10}";

        var reversal = BuildReversal(
            original, originalLines, reversalType, companyCode, period,
            postingDate, documentNumber, formatted, command);
        db.Add(reversal);

        // Permitted by fin.TR_JournalEntryHeader_PostedImmutable: the trigger
        // freezes accounts, amounts and dates on a posted document, and allows the
        // status transition and the one-time back-link that a reversal writes.
        original.ReversedByDocumentNumber = documentNumber;
        original.Status = JournalStatus.Reversed;

        var cleared = await ClearOpenItemsAsync(companyCode.Id, command, documentNumber, postingDate, ct);

        db.Add(new Audit.Domain.AuditLog
        {
            TenantId = tenant.TenantId,
            OccurredAtUtc = clock.UtcNow,
            UserName = user.UserName,
            CompanyCodeId = companyCode.Id,
            Action = Audit.Domain.AuditAction.Reverse,
            ObjectType = "JournalEntry",
            ObjectId = original.DocumentNumberFormatted,
            SourceApi = "POST /api/v1/finance/journal-entries/{...}/reverse",
            TransactionCode = "FB08",
            CorrelationId = correlation.CorrelationId,
            Summary = $"Reversed by {formatted}, reason {command.ReversalReasonCode}" +
                      (moved ? " (posting date moved to an open period)" : "") + ".",
        });

        return new ReverseJournalEntryResult
        {
            CompanyCode = companyCode.Code,
            ReversedDocumentNumber = original.DocumentNumber,
            ReversalDocumentNumber = documentNumber,
            ReversalDocumentNumberFormatted = formatted,
            ReversalPostingDate = postingDate,
            ReversalFiscalPeriod = period.Period,
            PostingDateMoved = moved,
            LinesReversed = originalLines.Count,
            OpenItemsCleared = cleared,
        };
    }

    private static void Validate(JournalEntryHeader original)
    {
        // The specific reason first: a reversed document also fails the status
        // check, and "already reversed by 100000009" is the answer the user needs.
        if (original.ReversedByDocumentNumber is { } already)
        {
            throw new BusinessRuleException(
                "ALREADY_REVERSED",
                $"Document {original.DocumentNumberFormatted} was already reversed by {already}.");
        }

        if (original.Status != JournalStatus.Posted)
        {
            throw new BusinessRuleException(
                "DOCUMENT_NOT_POSTED",
                $"Document {original.DocumentNumberFormatted} is {original.Status} and cannot be reversed.");
        }

        // A reversal chain nobody can follow is worse than re-posting the original.
        if (original.ReversalOfDocumentNumber is { } reverses)
        {
            throw new BusinessRuleException(
                "REVERSAL_OF_REVERSAL",
                $"Document {original.DocumentNumberFormatted} is itself the reversal of {reverses}. " +
                "Post the original entry again rather than reversing a reversal.");
        }

        if (!original.DocumentType.IsReversalAllowed)
        {
            throw new BusinessRuleException(
                "REVERSAL_NOT_ALLOWED",
                $"Document type {original.DocumentType.Code} does not permit reversal.");
        }
    }

    private async Task<(DateOnly PostingDate, FiscalPeriodResult Period, bool Moved)>
        ResolveReversalPeriodAsync(
            ReverseJournalEntryCommand command, CompanyCode companyCode,
            JournalEntryHeader original, List<JournalEntryLine> lines, CancellationToken ct)
    {
        var accountTypes = lines.Select(l => l.AccountType).Distinct().ToList();
        var requested = command.PostingDate ?? original.PostingDate;

        var period = await periods.DeriveAsync(companyCode.Id, requested, ct);
        try
        {
            await periods.RequireOpenAsync(companyCode.Id, period, accountTypes, ct);
            return (requested, period, false);
        }
        catch (BusinessRuleException) when (command.PostingDate is null)
        {
            // The original period is shut. Rather than blocking the correction,
            // move it to today's period and report that it moved.
            var today = DateOnly.FromDateTime(clock.UtcNow);
            var fallback = await periods.DeriveAsync(companyCode.Id, today, ct);
            await periods.RequireOpenAsync(companyCode.Id, fallback, accountTypes, ct);
            return (today, fallback, true);
        }
    }

    private async Task<DocumentType> ResolveReversalDocumentTypeAsync(
        JournalEntryHeader original, CancellationToken ct)
    {
        var code = original.DocumentType.ReversalDocumentTypeCode;
        if (code is null)
        {
            return original.DocumentType;
        }

        return await db.Set<DocumentType>().SingleOrDefaultAsync(d => d.Code == code, ct)
               ?? original.DocumentType;
    }

    private JournalEntryHeader BuildReversal(
        JournalEntryHeader original, List<JournalEntryLine> lines, DocumentType reversalType,
        CompanyCode companyCode, FiscalPeriodResult period, DateOnly postingDate,
        long documentNumber, string formatted, ReverseJournalEntryCommand command)
    {
        var reversal = new JournalEntryHeader
        {
            TenantId = tenant.TenantId,
            CompanyCodeId = companyCode.Id,
            FiscalYear = period.FiscalYear,
            DocumentNumber = documentNumber,
            DocumentNumberFormatted = formatted,
            DocumentTypeId = reversalType.Id,
            DocumentDate = postingDate,
            PostingDate = postingDate,
            FiscalPeriod = period.Period,
            EntryDateUtc = clock.UtcNow,
            DocumentCurrencyId = original.DocumentCurrencyId,
            ExchangeRateTypeId = original.ExchangeRateTypeId,
            // The original rate, not today's: a reversal must undo exactly what
            // was posted, not introduce a translation difference.
            ExchangeRateToLocal = original.ExchangeRateToLocal,
            ExchangeRateToGroup = original.ExchangeRateToGroup,
            Reference = original.Reference,
            HeaderText = $"Reversal of {original.DocumentNumberFormatted}",
            Status = JournalStatus.Posted,
            SourceModule = original.SourceModule,
            TransactionCode = "FB08",
            ReversalOfDocumentNumber = original.DocumentNumber,
            ReversalReasonCode = command.ReversalReasonCode,
            IntercompanyTransactionId = original.IntercompanyTransactionId,
            IdempotencyKey = command.IdempotencyKey,
            CorrelationId = correlation.CorrelationId,
            CreatedBy = user.UserName,
            CreatedAtUtc = clock.UtcNow,
            PostedBy = user.UserName,
            PostedAtUtc = clock.UtcNow,
        };

        foreach (var line in lines)
        {
            reversal.Lines.Add(new JournalEntryLine
            {
                TenantId = tenant.TenantId,
                CompanyCodeId = companyCode.Id,
                FiscalYear = period.FiscalYear,
                DocumentNumber = documentNumber,
                LedgerId = line.LedgerId,
                LineNumber = line.LineNumber,
                // The mirror: opposite indicator, negated amounts, everything else
                // identical so the two documents net to nothing on every dimension.
                PostingKey = line.PostingKey,
                DebitCredit = line.DebitCredit == DebitCredit.Debit
                    ? DebitCredit.Credit
                    : DebitCredit.Debit,
                AccountType = line.AccountType,
                GLAccountId = line.GLAccountId,
                BusinessPartnerId = line.BusinessPartnerId,
                BusinessPartnerRole = line.BusinessPartnerRole,
                AssetId = line.AssetId,
                AssetSubNumber = line.AssetSubNumber,
                CostCenterId = line.CostCenterId,
                ProfitCenterId = line.ProfitCenterId,
                InternalOrderId = line.InternalOrderId,
                BusinessAreaId = line.BusinessAreaId,
                FunctionalAreaId = line.FunctionalAreaId,
                SegmentId = line.SegmentId,
                PlantId = line.PlantId,
                BranchId = line.BranchId,
                PartnerCompanyCodeId = line.PartnerCompanyCodeId,
                PartnerProfitCenterId = line.PartnerProfitCenterId,
                PartnerSegmentId = line.PartnerSegmentId,
                DocumentAmount = -line.DocumentAmount,
                DocumentCurrencyId = line.DocumentCurrencyId,
                LocalAmount = -line.LocalAmount,
                LocalCurrencyId = line.LocalCurrencyId,
                GroupAmount = -line.GroupAmount,
                GroupCurrencyId = line.GroupCurrencyId,
                Quantity = -line.Quantity,
                UnitOfMeasure = line.UnitOfMeasure,
                TaxCode = line.TaxCode,
                TaxBaseAmount = -line.TaxBaseAmount,
                TaxAmount = -line.TaxAmount,
                IsTaxLine = line.IsTaxLine,
                Assignment = line.Assignment,
                LineText = $"Reversal of {original.DocumentNumber}/{line.LineNumber}",
                SourceModule = line.SourceModule,
                SourceDocumentType = original.DocumentType.Code,
                SourceDocumentNumber = original.DocumentNumber,
                SourceLineNumber = line.LineNumber,
            });
        }

        return reversal;
    }

    /// <summary>
    /// Clears the original's open items against the reversal instead of creating
    /// mirrored open items. Two offsetting open items would both appear on an
    /// aging report, which is not what the counterparty owes.
    /// </summary>
    private async Task<int> ClearOpenItemsAsync(
        long companyCodeId, ReverseJournalEntryCommand command,
        long reversalDocumentNumber, DateOnly clearingDate, CancellationToken ct)
    {
        var items = await db.Set<OpenItem>()
            .Where(o => o.CompanyCodeId == companyCodeId
                        && o.FiscalYear == command.FiscalYear
                        && o.DocumentNumber == command.DocumentNumber
                        && o.ClearingStatus != ClearingStatus.Cleared)
            .ToListAsync(ct);

        foreach (var item in items)
        {
            item.OpenAmountDocument = 0m;
            item.OpenAmountLocal = 0m;
            item.ClearingStatus = ClearingStatus.Cleared;
            item.ClearingDocumentNumber = reversalDocumentNumber;
            item.ClearingDate = clearingDate;
        }

        return items.Count;
    }
}
