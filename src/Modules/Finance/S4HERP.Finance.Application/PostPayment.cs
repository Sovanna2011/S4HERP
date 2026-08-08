using Microsoft.EntityFrameworkCore;
using S4HERP.BuildingBlocks.Application;
using S4HERP.BuildingBlocks.Infrastructure;
using S4HERP.Finance.Domain;
using S4HERP.Organization.Application;
using S4HERP.Organization.Domain;

namespace S4HERP.Finance.Application;

// ----------------------------------------------------------------- commands

/// <summary>
/// F-28 and F-53: an incoming or outgoing payment that clears open items.
///
/// The payment amount is not supplied — it is the sum of what the caller chose
/// to clear. That is the difference between a payment application and a bank
/// statement: here the operator is saying "this money settles these invoices",
/// and a total that disagreed with the selection would either post an
/// unexplained difference or silently leave money unapplied.
/// </summary>
[RequiresAuthorization("F_BKPF_BUK", "01")]
public sealed record PostPaymentCommand : ICommand<PostPaymentResult>
{
    public required string CompanyCode { get; init; }
    public required DateOnly PostingDate { get; init; }
    public DateOnly? DocumentDate { get; init; }

    /// <summary>The partner being paid, or paying.</summary>
    public required string BusinessPartner { get; init; }

    /// <summary>Bank or cash G/L account the money moves through.</summary>
    public required string BankAccount { get; init; }

    /// <summary>Which open items this payment settles, and how much of each.</summary>
    public required IReadOnlyList<PaymentItemSelection> Items { get; init; }

    public string? Reference { get; init; }
    public string? HeaderText { get; init; }
    public string? IdempotencyKey { get; init; }
}

public sealed record PaymentItemSelection
{
    public required short FiscalYear { get; init; }
    public required long DocumentNumber { get; init; }
    public required short LineNumber { get; init; }

    /// <summary>
    /// Omitted means "settle it in full". A smaller amount leaves the remainder
    /// open — a partial payment, not a written-off one.
    /// </summary>
    public decimal? Amount { get; init; }
}

public sealed record PostPaymentResult
{
    public required string CompanyCode { get; init; }
    public required short FiscalYear { get; init; }
    public required long DocumentNumber { get; init; }
    public required string DocumentNumberFormatted { get; init; }
    public required string DocumentType { get; init; }
    public required string Direction { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required int ItemsCleared { get; init; }
    public required int ItemsPartiallyCleared { get; init; }
    public required IReadOnlyList<ClearedItemView> Cleared { get; init; }
}

public sealed record ClearedItemView(
    short FiscalYear,
    long DocumentNumber,
    short LineNumber,
    decimal ClearedAmount,
    decimal RemainingOpen,
    string ClearingStatus);

/// <summary>
/// FBRA. A clearing applied to the wrong invoice is an ordinary mistake, and
/// without a reset the only way out would be reversing the payment itself.
/// </summary>
[RequiresAuthorization("F_BKPF_BUK", "01")]
public sealed record ResetClearingCommand : ICommand<ResetClearingResult>
{
    public required string CompanyCode { get; init; }
    public required short FiscalYear { get; init; }

    /// <summary>The clearing (payment) document, not the invoice.</summary>
    public required long ClearingDocumentNumber { get; init; }
}

public sealed record ResetClearingResult
{
    public required string CompanyCode { get; init; }
    public required long ClearingDocumentNumber { get; init; }
    public required int ItemsReopened { get; init; }
}

internal static class PaymentErrors
{
    public const string NoItems = "NO_ITEMS_SELECTED";
    public const string ItemNotOpen = "ITEM_NOT_OPEN";
    public const string ItemNotFound = "OPEN_ITEM_NOT_FOUND";
    public const string OverClearing = "CLEARING_EXCEEDS_OPEN_AMOUNT";
    public const string MixedPartners = "ITEMS_SPAN_PARTNERS";
    public const string MixedDirection = "ITEMS_SPAN_DIRECTIONS";
    public const string MixedCurrency = "ITEMS_SPAN_CURRENCIES";
    public const string NotCleared = "CLEARING_NOT_FOUND";
    public const string AlreadyReset = "CLEARING_ALREADY_RESET";
}

// ------------------------------------------------------------------ handler

public sealed class PostPaymentHandler(
    S4herpDbContext db,
    IFiscalPeriodService periods,
    ICurrencyTranslator currencies,
    INumberRangeAllocator numbers,
    IAuthorizationEnforcer authorization,
    IUserContext user,
    ITenantContext tenant,
    IClock clock,
    ICorrelationContext correlation)
    : ICommandHandler<PostPaymentCommand, PostPaymentResult>
{
    public async Task<PostPaymentResult> HandleAsync(
        PostPaymentCommand command, CancellationToken ct)
    {
        if (command.Items.Count == 0)
        {
            throw new BusinessRuleException(
                PaymentErrors.NoItems, "A payment must settle at least one open item.");
        }

        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", command.CompanyCode), ("ACTVT", "01")], ct);

        var companyCode = await db.Set<CompanyCode>()
            .SingleOrDefaultAsync(c => c.Code == command.CompanyCode, ct)
            ?? throw new NotFoundException($"Company code {command.CompanyCode} does not exist.");

        var partner = await db.Set<BusinessPartner.Domain.Partner>()
            .SingleOrDefaultAsync(p => p.PartnerNumber == command.BusinessPartner, ct)
            ?? throw new NotFoundException(
                $"Business partner {command.BusinessPartner} does not exist.");

        var bankAccount = await db.Set<GLAccount>()
            .SingleOrDefaultAsync(a => a.ChartOfAccountsId == companyCode.ChartOfAccountsId
                                       && a.AccountNumber == command.BankAccount, ct)
            ?? throw new NotFoundException($"G/L account {command.BankAccount} does not exist.");

        if (bankAccount.IsReconciliationAccount)
        {
            throw new BusinessRuleException(
                PostingErrors.ReconciliationAccount,
                $"G/L account {command.BankAccount} is a reconciliation account and cannot be " +
                "the bank side of a payment.");
        }

        var selected = await LoadSelectedItemsAsync(command, companyCode.Id, partner.Id, ct);
        var (accountType, currencyId) = ValidateHomogeneous(selected);

        var documentType = await ResolveDocumentTypeAsync(accountType, ct);
        await authorization.RequireAsync(
            "F_BKPF_BLA", [("BLART", documentType.Code), ("ACTVT", "01")], ct);

        var period = await periods.DeriveAsync(companyCode.Id, command.PostingDate, ct);
        await periods.RequireOpenAsync(
            companyCode.Id, period, [accountType, AccountType.GeneralLedger], ct);

        var currency = await db.Set<Currency>().SingleAsync(c => c.Id == currencyId, ct);
        var localCurrency = await db.Set<Currency>()
            .SingleAsync(c => c.Id == companyCode.LocalCurrencyId, ct);
        var rateType = await db.Set<ExchangeRateType>().FirstAsync(t => t.Code == "M", ct);
        var rate = currencyId == companyCode.LocalCurrencyId
            ? 1m
            : await currencies.RateAsync(currencyId, companyCode.LocalCurrencyId,
                command.PostingDate, "M", ct);

        var ledger = await db.Set<Ledger>()
            .SingleAsync(l => l.LedgerType == LedgerType.Leading && l.IsActive, ct);

        var documentNumber = await numbers.AllocateAsync(
            NumberRangeObject.AccountingDocument, documentType.NumberRangeCode,
            companyCode.Id, period.FiscalYear, ct);

        var formatted =
            $"KSS-{companyCode.Code}-{period.FiscalYear}-{documentType.Code}-{documentNumber:D10}";

        // A customer open item is a debit (they owe us), so settling it credits
        // the customer and debits the bank. A vendor item is the mirror.
        var incoming = accountType == AccountType.Customer;
        var total = selected.Sum(s => s.ClearAmount);

        var header = BuildHeader(
            command, companyCode, documentType, period, documentNumber, formatted,
            currencyId, rateType.Id, rate);

        // The bank line takes the sign that makes the document balance: the
        // partner line always offsets the open items it settles.
        var bankSigned = incoming ? total : -total;

        header.Lines.Add(NewLine(companyCode, period, documentNumber, ledger.Id, 1,
            incoming ? "40" : "50", incoming ? DebitCredit.Debit : DebitCredit.Credit,
            AccountType.GeneralLedger, bankAccount.Id, null, null,
            bankSigned, currencyId, currencies.Translate(bankSigned, rate), localCurrency.Id,
            documentType.Code, "Payment"));

        var partnerSigned = -bankSigned;
        var reconciliation = selected[0].Item.GLAccountId;
        header.Lines.Add(NewLine(companyCode, period, documentNumber, ledger.Id, 2,
            incoming ? "15" : "25", incoming ? DebitCredit.Credit : DebitCredit.Debit,
            accountType, reconciliation, partner.Id,
            incoming ? "FI_CUST" : "FI_VEND",
            partnerSigned, currencyId, currencies.Translate(partnerSigned, rate), localCurrency.Id,
            documentType.Code, "Payment"));

        db.Add(header);

        // The payment's own partner line is an open item too, cleared by this same
        // clearing. Without it the subledger simply loses sight of the payment:
        // the journal's AR balance drops by the payment but the open items do not
        // reflect where it went, and a clearing reset then leaves the two
        // permanently apart. Phase 2's reconciliation rule caught exactly that.
        var paymentItem = new OpenItem
        {
            TenantId = tenant.TenantId,
            CompanyCodeId = companyCode.Id,
            FiscalYear = period.FiscalYear,
            DocumentNumber = documentNumber,
            LedgerId = ledger.Id,
            LineNumber = 2,
            AccountType = accountType,
            BusinessPartnerId = partner.Id,
            BusinessPartnerRole = incoming ? "FI_CUST" : "FI_VEND",
            GLAccountId = reconciliation,
            OriginalAmountDocument = partnerSigned,
            OpenAmountDocument = partnerSigned,
            DocumentCurrencyId = currencyId,
            OriginalAmountLocal = currencies.Translate(partnerSigned, rate),
            OpenAmountLocal = currencies.Translate(partnerSigned, rate),
            LocalCurrencyId = localCurrency.Id,
            ClearingStatus = ClearingStatus.Open,
        };
        db.Add(paymentItem);

        var cleared = ApplyClearing(selected, paymentItem, total, companyCode.Id,
            period.FiscalYear, documentNumber, command.PostingDate, currencyId, rate);

        if (command.IdempotencyKey is { Length: > 0 } key)
        {
            db.Add(new PostingIdempotency
            {
                TenantId = tenant.TenantId,
                IdempotencyKey = key,
                CompanyCodeId = companyCode.Id,
                FiscalYear = period.FiscalYear,
                DocumentNumber = documentNumber,
                CreatedAtUtc = clock.UtcNow,
            });
        }

        db.Add(new Audit.Domain.AuditLog
        {
            TenantId = tenant.TenantId,
            OccurredAtUtc = clock.UtcNow,
            UserName = user.UserName,
            CompanyCodeId = companyCode.Id,
            Action = Audit.Domain.AuditAction.Post,
            ObjectType = "Payment",
            ObjectId = formatted,
            SourceApi = "POST /api/v1/finance/payments",
            TransactionCode = incoming ? "F-28" : "F-53",
            CorrelationId = correlation.CorrelationId,
            Summary = $"{(incoming ? "Incoming" : "Outgoing")} payment {total:N2} " +
                      $"{currency.Code} clearing {cleared.Count} item(s).",
        });

        return new PostPaymentResult
        {
            CompanyCode = companyCode.Code,
            FiscalYear = period.FiscalYear,
            DocumentNumber = documentNumber,
            DocumentNumberFormatted = formatted,
            DocumentType = documentType.Code,
            Direction = incoming ? "Incoming" : "Outgoing",
            Amount = total,
            Currency = currency.Code,
            ItemsCleared = cleared.Count(c => c.ClearingStatus == nameof(ClearingStatus.Cleared)),
            ItemsPartiallyCleared =
                cleared.Count(c => c.ClearingStatus == nameof(ClearingStatus.PartiallyCleared)),
            Cleared = cleared,
        };
    }

    private sealed record Selection(OpenItem Item, decimal ClearAmount);

    private async Task<List<Selection>> LoadSelectedItemsAsync(
        PostPaymentCommand command, long companyCodeId, long partnerId, CancellationToken ct)
    {
        var years = command.Items.Select(i => i.FiscalYear).Distinct().ToList();
        var documents = command.Items.Select(i => i.DocumentNumber).Distinct().ToList();

        var candidates = await db.Set<OpenItem>()
            .Where(o => o.CompanyCodeId == companyCodeId
                        && years.Contains(o.FiscalYear)
                        && documents.Contains(o.DocumentNumber))
            .ToListAsync(ct);

        var violations = new List<RuleViolation>();
        var selected = new List<Selection>();

        for (var i = 0; i < command.Items.Count; i++)
        {
            var wanted = command.Items[i];
            var field = $"items[{i}]";

            var item = candidates.SingleOrDefault(
                o => o.FiscalYear == wanted.FiscalYear
                     && o.DocumentNumber == wanted.DocumentNumber
                     && o.LineNumber == wanted.LineNumber);

            if (item is null)
            {
                violations.Add(new RuleViolation(field, PaymentErrors.ItemNotFound,
                    $"No open item {wanted.DocumentNumber}/{wanted.LineNumber} in fiscal year " +
                    $"{wanted.FiscalYear}."));
                continue;
            }

            if (item.ClearingStatus == ClearingStatus.Cleared)
            {
                violations.Add(new RuleViolation(field, PaymentErrors.ItemNotOpen,
                    $"Item {wanted.DocumentNumber}/{wanted.LineNumber} is already cleared by " +
                    $"document {item.ClearingDocumentNumber}."));
                continue;
            }

            if (item.BusinessPartnerId != partnerId)
            {
                violations.Add(new RuleViolation(field, PaymentErrors.MixedPartners,
                    $"Item {wanted.DocumentNumber}/{wanted.LineNumber} belongs to a different " +
                    "business partner."));
                continue;
            }

            // Open amounts carry the item's sign; the caller thinks in magnitudes.
            var open = Math.Abs(item.OpenAmountDocument);
            var amount = wanted.Amount is { } requested ? Math.Abs(requested) : open;

            if (amount == 0)
            {
                violations.Add(new RuleViolation(field, PaymentErrors.OverClearing,
                    $"Item {wanted.DocumentNumber}/{wanted.LineNumber}: nothing to clear."));
                continue;
            }

            if (amount > open)
            {
                // Refused rather than absorbed. Over-payment is a real business
                // event that needs its own posting, not a rounding of this one.
                violations.Add(new RuleViolation(field, PaymentErrors.OverClearing,
                    $"Item {wanted.DocumentNumber}/{wanted.LineNumber} has {open:N2} open; " +
                    $"cannot clear {amount:N2}."));
                continue;
            }

            selected.Add(new Selection(item, amount));
        }

        if (violations.Count > 0)
        {
            throw new BusinessRuleException(
                PaymentErrors.ItemNotFound, "The payment selection is not valid.", violations);
        }

        return selected;
    }

    /// <summary>
    /// One payment document settles one partner, one direction, one currency.
    /// Mixing any of them would need several partner lines and several clearing
    /// groups, which is a different transaction, not a bigger one.
    /// </summary>
    private static (AccountType AccountType, long CurrencyId) ValidateHomogeneous(
        List<Selection> selected)
    {
        var accountTypes = selected.Select(s => s.Item.AccountType).Distinct().ToList();
        if (accountTypes.Count > 1)
        {
            throw new BusinessRuleException(
                PaymentErrors.MixedDirection,
                "A payment cannot settle customer and vendor items in one document.");
        }

        var currencies = selected.Select(s => s.Item.DocumentCurrencyId).Distinct().ToList();
        if (currencies.Count > 1)
        {
            throw new BusinessRuleException(
                PaymentErrors.MixedCurrency,
                "A payment cannot settle items in different currencies in one document.");
        }

        return (accountTypes[0], currencies[0]);
    }

    private async Task<DocumentType> ResolveDocumentTypeAsync(
        AccountType accountType, CancellationToken ct)
    {
        var code = accountType == AccountType.Customer ? "DZ" : "KZ";
        return await db.Set<DocumentType>().SingleOrDefaultAsync(d => d.Code == code, ct)
               ?? throw new BusinessRuleException(
                   PostingErrors.UnknownObject, $"Document type {code} is not configured.");
    }

    private List<ClearedItemView> ApplyClearing(
        List<Selection> selected, OpenItem paymentItem, decimal paymentTotal,
        long companyCodeId, short fiscalYear,
        long clearingDocumentNumber, DateOnly clearingDate, long currencyId, decimal rate)
    {
        var clearing = new ClearingHeader
        {
            TenantId = tenant.TenantId,
            CompanyCodeId = companyCodeId,
            FiscalYear = fiscalYear,
            ClearingDocumentNumber = clearingDocumentNumber,
            ClearingDate = clearingDate,
            CurrencyId = currencyId,
            CreatedBy = user.UserName,
            CreatedAtUtc = clock.UtcNow,
        };

        var views = new List<ClearedItemView>();

        foreach (var (item, amount) in selected)
        {
            // Reduce the magnitude, keep the sign. An open item that changed sign
            // on partial payment would flip a receivable into a payable.
            var sign = Math.Sign(item.OpenAmountDocument);
            var remaining = Math.Abs(item.OpenAmountDocument) - amount;
            var localAmount = currencies.Translate(amount, rate);

            item.OpenAmountDocument = sign * remaining;
            item.OpenAmountLocal = sign * Math.Round(
                Math.Abs(item.OpenAmountLocal) - Math.Abs(localAmount), 4,
                MidpointRounding.AwayFromZero);

            if (remaining == 0)
            {
                item.ClearingStatus = ClearingStatus.Cleared;
                item.ClearingDocumentNumber = clearingDocumentNumber;
                item.ClearingDate = clearingDate;
            }
            else
            {
                // Deliberately no clearing document on a partial: the item is still
                // open, and naming a clearing document for it would make an aging
                // report believe it was settled.
                item.ClearingStatus = ClearingStatus.PartiallyCleared;
            }

            clearing.Lines.Add(new ClearingLine
            {
                TenantId = tenant.TenantId,
                OpenItemId = item.Id,
                ClearedAmountDocument = amount,
                ClearedAmountLocal = Math.Abs(localAmount),
                IsResidual = remaining > 0,
            });

            views.Add(new ClearedItemView(
                item.FiscalYear, item.DocumentNumber, item.LineNumber,
                amount, remaining, item.ClearingStatus.ToString()));
        }

        // The payment side is settled in full by construction: the document's value
        // is the sum of what it clears, so there is never a remainder on it.
        paymentItem.OpenAmountDocument = 0m;
        paymentItem.OpenAmountLocal = 0m;
        paymentItem.ClearingStatus = ClearingStatus.Cleared;
        paymentItem.ClearingDocumentNumber = clearingDocumentNumber;
        paymentItem.ClearingDate = clearingDate;

        clearing.Lines.Add(new ClearingLine
        {
            TenantId = tenant.TenantId,
            OpenItem = paymentItem,
            ClearedAmountDocument = paymentTotal,
            ClearedAmountLocal = Math.Abs(currencies.Translate(paymentTotal, rate)),
            IsResidual = false,
        });

        db.Add(clearing);
        return views;
    }

    private JournalEntryHeader BuildHeader(
        PostPaymentCommand command, CompanyCode companyCode, DocumentType documentType,
        FiscalPeriodResult period, long documentNumber, string formatted,
        long currencyId, long rateTypeId, decimal rate) => new()
        {
            TenantId = tenant.TenantId,
            CompanyCodeId = companyCode.Id,
            FiscalYear = period.FiscalYear,
            DocumentNumber = documentNumber,
            DocumentNumberFormatted = formatted,
            DocumentTypeId = documentType.Id,
            DocumentDate = command.DocumentDate ?? command.PostingDate,
            PostingDate = command.PostingDate,
            FiscalPeriod = period.Period,
            EntryDateUtc = clock.UtcNow,
            DocumentCurrencyId = currencyId,
            ExchangeRateTypeId = rateTypeId,
            ExchangeRateToLocal = rate,
            ExchangeRateToGroup = 1m,
            Reference = command.Reference,
            HeaderText = command.HeaderText,
            Status = JournalStatus.Posted,
            SourceModule = documentType.Code == "DZ"
                ? SourceModule.AccountsReceivable
                : SourceModule.AccountsPayable,
            TransactionCode = documentType.Code == "DZ" ? "F-28" : "F-53",
            IdempotencyKey = command.IdempotencyKey,
            CorrelationId = correlation.CorrelationId,
            CreatedBy = user.UserName,
            CreatedAtUtc = clock.UtcNow,
            PostedBy = user.UserName,
            PostedAtUtc = clock.UtcNow,
        };

    private JournalEntryLine NewLine(
        CompanyCode companyCode, FiscalPeriodResult period, long documentNumber, long ledgerId,
        short lineNumber, string postingKey, DebitCredit debitCredit, AccountType accountType,
        long? glAccountId, long? partnerId, string? partnerRole,
        decimal documentAmount, long documentCurrencyId, decimal localAmount, long localCurrencyId,
        string sourceDocumentType, string text) => new()
        {
            TenantId = tenant.TenantId,
            CompanyCodeId = companyCode.Id,
            FiscalYear = period.FiscalYear,
            DocumentNumber = documentNumber,
            LedgerId = ledgerId,
            LineNumber = lineNumber,
            PostingKey = postingKey,
            DebitCredit = debitCredit,
            AccountType = accountType,
            GLAccountId = glAccountId,
            BusinessPartnerId = partnerId,
            BusinessPartnerRole = partnerRole,
            DocumentAmount = documentAmount,
            DocumentCurrencyId = documentCurrencyId,
            LocalAmount = localAmount,
            LocalCurrencyId = localCurrencyId,
            LineText = text,
            SourceModule = sourceDocumentType == "DZ"
                ? SourceModule.AccountsReceivable
                : SourceModule.AccountsPayable,
        };
}

public sealed class ResetClearingHandler(
    S4herpDbContext db,
    IAuthorizationEnforcer authorization,
    IUserContext user,
    ITenantContext tenant,
    IClock clock,
    ICorrelationContext correlation)
    : ICommandHandler<ResetClearingCommand, ResetClearingResult>
{
    public async Task<ResetClearingResult> HandleAsync(
        ResetClearingCommand command, CancellationToken ct)
    {
        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", command.CompanyCode), ("ACTVT", "01")], ct);

        var companyCode = await db.Set<CompanyCode>()
            .SingleOrDefaultAsync(c => c.Code == command.CompanyCode, ct)
            ?? throw new NotFoundException($"Company code {command.CompanyCode} does not exist.");

        var clearing = await db.Set<ClearingHeader>()
            .Include(c => c.Lines)
            .SingleOrDefaultAsync(c => c.CompanyCodeId == companyCode.Id
                                       && c.FiscalYear == command.FiscalYear
                                       && c.ClearingDocumentNumber == command.ClearingDocumentNumber, ct)
            ?? throw new BusinessRuleException(
                PaymentErrors.NotCleared,
                $"Document {command.ClearingDocumentNumber} cleared nothing in fiscal year " +
                $"{command.FiscalYear}.");

        if (clearing.IsReset)
        {
            throw new BusinessRuleException(
                PaymentErrors.AlreadyReset,
                $"The clearing by document {command.ClearingDocumentNumber} was already reset.");
        }

        var itemIds = clearing.Lines.Select(l => l.OpenItemId).ToList();
        var items = await db.Set<OpenItem>().Where(o => itemIds.Contains(o.Id)).ToListAsync(ct);

        foreach (var line in clearing.Lines)
        {
            var item = items.Single(o => o.Id == line.OpenItemId);
            var sign = Math.Sign(item.OriginalAmountDocument);

            // Give back exactly what this clearing took, rather than resetting to
            // the original amount: another clearing may also be holding part of
            // this item, and that one is not being reset.
            item.OpenAmountDocument += sign * line.ClearedAmountDocument;
            item.OpenAmountLocal += sign * line.ClearedAmountLocal;

            item.ClearingStatus =
                Math.Abs(item.OpenAmountDocument) == Math.Abs(item.OriginalAmountDocument)
                    ? ClearingStatus.Open
                    : ClearingStatus.PartiallyCleared;
            item.ClearingDocumentNumber = null;
            item.ClearingDate = null;
        }

        clearing.IsReset = true;
        clearing.ResetAtUtc = clock.UtcNow;

        db.Add(new Audit.Domain.AuditLog
        {
            TenantId = tenant.TenantId,
            OccurredAtUtc = clock.UtcNow,
            UserName = user.UserName,
            CompanyCodeId = companyCode.Id,
            Action = Audit.Domain.AuditAction.Change,
            ObjectType = "Clearing",
            ObjectId = command.ClearingDocumentNumber.ToString(),
            SourceApi = "POST /api/v1/finance/payments/{...}/reset-clearing",
            TransactionCode = "FBRA",
            CorrelationId = correlation.CorrelationId,
            Summary = $"Clearing reset; {items.Count} item(s) reopened. " +
                      "The payment document itself is untouched.",
        });

        return new ResetClearingResult
        {
            CompanyCode = companyCode.Code,
            ClearingDocumentNumber = command.ClearingDocumentNumber,
            ItemsReopened = items.Count,
        };
    }
}
