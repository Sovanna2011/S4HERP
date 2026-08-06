using S4HERP.BuildingBlocks.Application;
using S4HERP.Finance.Domain;

namespace S4HERP.Finance.Application;

/// <summary>
/// Posts an accounting document. Every financial posting in the system — from
/// AR, AP, Assets, Controlling or Integration — arrives here.
/// </summary>
[RequiresAuthorization("F_BKPF_BUK", "01")]
public sealed record PostJournalEntryCommand : ICommand<PostJournalEntryResult>
{
    public required string CompanyCode { get; init; }
    public required string DocumentType { get; init; }
    public required DateOnly DocumentDate { get; init; }
    public required DateOnly PostingDate { get; init; }
    public required string Currency { get; init; }

    /// <summary>Omitted means "look it up for the posting date".</summary>
    public decimal? ExchangeRate { get; init; }

    public string? Reference { get; init; }
    public string? HeaderText { get; init; }

    public required IReadOnlyList<JournalLineInput> Lines { get; init; }

    /// <summary>
    /// Deduplicates retries. A replayed request returns the original document
    /// rather than posting a second one (§22.1).
    /// </summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>
    /// Runs the whole pipeline up to and including the balance check and returns
    /// the derived result without allocating a number or writing anything.
    /// </summary>
    public bool Simulate { get; init; }

    /// <summary>
    /// FV50. Writes the document with status <c>Parked</c>: it takes a document
    /// number and its lines are final, but it does not hit the ledger — no open
    /// items, and the trial balance ignores it. Submitting it for approval and
    /// having that approval completed is what posts it.
    ///
    /// A flag rather than a separate command, for the same reason
    /// <see cref="Simulate"/> is: park, simulate and post must derive the document
    /// through identical code, or parking would approve one document and post
    /// another.
    /// </summary>
    public bool Park { get; init; }
}

public sealed record JournalLineInput
{
    public required string PostingKey { get; init; }
    public required decimal Amount { get; init; }

    /// <summary>G/L account number. Required for account type S.</summary>
    public string? GLAccount { get; init; }

    /// <summary>Business partner number. Required for account types D and K.</summary>
    public string? BusinessPartner { get; init; }

    public string? CostCenter { get; init; }
    public string? ProfitCenter { get; init; }
    public string? InternalOrder { get; init; }
    public string? Segment { get; init; }
    public string? PartnerCompanyCode { get; init; }

    public string? TaxCode { get; init; }
    public string? Assignment { get; init; }
    public string? LineText { get; init; }
    public DateOnly? DueDate { get; init; }
    public string? PaymentTerms { get; init; }
}

public sealed record PostJournalEntryResult
{
    public required bool Posted { get; init; }

    /// <summary>
    /// The document's status — <c>Simulated</c>, <c>Parked</c> or <c>Posted</c>.
    /// <see cref="Posted"/> alone cannot distinguish "not written" from "written
    /// but not in the ledger".
    /// </summary>
    public string Status { get; init; } = "Simulated";

    public required string CompanyCode { get; init; }
    public required short FiscalYear { get; init; }
    public required byte FiscalPeriod { get; init; }
    public long? DocumentNumber { get; init; }
    public string? DocumentNumberFormatted { get; init; }

    /// <summary>True when an idempotency key matched an existing document.</summary>
    public bool WasReplay { get; init; }

    public required IReadOnlyList<DerivedLine> Lines { get; init; }
    public required IReadOnlyList<CurrencyTotal> Totals { get; init; }
}

/// <summary>
/// A line after derivation, translation and tax. What simulation shows and what
/// posting writes are produced by the same pass — a simulation that runs
/// different code is a simulation of the wrong thing.
/// </summary>
public sealed record DerivedLine
{
    public required short LineNumber { get; init; }
    public required string PostingKey { get; init; }
    public required string DebitCredit { get; init; }
    public required string AccountType { get; init; }
    public string? GLAccount { get; init; }
    public string? BusinessPartner { get; init; }
    public string? CostCenter { get; init; }
    public string? ProfitCenter { get; init; }
    public string? Segment { get; init; }
    public string? PartnerCompanyCode { get; init; }
    public required decimal DocumentAmount { get; init; }
    public required string DocumentCurrency { get; init; }
    public required decimal LocalAmount { get; init; }
    public required string LocalCurrency { get; init; }
    public string? TaxCode { get; init; }
    public bool IsGenerated { get; init; }
    public string? LineText { get; init; }
}

public sealed record CurrencyTotal(string Currency, decimal Debit, decimal Credit, decimal Difference);

internal static class PostingErrors
{
    public const string PeriodClosed = "PERIOD_CLOSED";
    public const string Unbalanced = "DOCUMENT_UNBALANCED";
    public const string ReconciliationAccount = "RECONCILIATION_ACCOUNT_DIRECT_POSTING";
    public const string AccountBlocked = "ACCOUNT_BLOCKED";
    public const string UnknownObject = "UNKNOWN_OBJECT";
    public const string PostingKeyMismatch = "POSTING_KEY_MISMATCH";
    public const string DocumentTypeNotAllowed = "DOCUMENT_TYPE_ACCOUNT_TYPE_NOT_ALLOWED";
    public const string CostObjectRequired = "COST_OBJECT_REQUIRED";
    public const string NumberRangeExhausted = "NUMBER_RANGE_EXHAUSTED";
    public const string NoExchangeRate = "NO_EXCHANGE_RATE";
    public const string NotParked = "DOCUMENT_NOT_PARKED";
    public const string NotPendingApproval = "DOCUMENT_NOT_PENDING_APPROVAL";
}
