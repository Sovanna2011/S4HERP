using System.ComponentModel.DataAnnotations;
using S4HERP.BuildingBlocks.Domain;
using S4HERP.Organization.Domain;

namespace S4HERP.Finance.Domain;

public enum JournalStatus
{
    Draft = 1,
    Held = 2,
    Parked = 3,
    Submitted = 4,
    PendingApproval = 5,
    Approved = 6,
    Rejected = 7,
    Posted = 8,
    Reversed = 9,
    Cancelled = 10,
}

public enum DebitCredit
{
    Debit = 'D',
    Credit = 'C',
}

public enum ClearingStatus
{
    Open = 1,
    PartiallyCleared = 2,
    Cleared = 3,
}

public enum SourceModule
{
    GeneralLedger = 1,
    AccountsReceivable = 2,
    AccountsPayable = 3,
    AssetAccounting = 4,
    Controlling = 5,
    Integration = 6,
}

/// <summary>
/// Accounting document header. Mutable only while pre-posting; once
/// <see cref="Status"/> is <see cref="JournalStatus.Posted"/> the only permitted
/// change is the one-time write of <see cref="ReversedByDocumentNumber"/>.
/// </summary>
public class JournalEntryHeader : DomainEntity, ITenantScoped, IAuditable, IConcurrencyControlled
{
    // Composite business key, clustered.
    public long TenantId { get; set; }
    public long CompanyCodeId { get; set; }
    public short FiscalYear { get; set; }

    /// <summary>
    /// Numeric document number, unique within (tenant, company code, fiscal year)
    /// because number range intervals do not overlap. Kept numeric to hold the
    /// clustered key of the journal narrow.
    /// </summary>
    public long DocumentNumber { get; set; }

    /// <summary>
    /// Presentation form, e.g. KSS-1000-2026-SA-0000000123. Carries the company
    /// code because a document number is only unique within one.
    /// </summary>
    [MaxLength(30)] public required string DocumentNumberFormatted { get; set; }

    public CompanyCode CompanyCode { get; set; } = null!;

    public long DocumentTypeId { get; set; }
    public DocumentType DocumentType { get; set; } = null!;

    public DateOnly DocumentDate { get; set; }
    public DateOnly PostingDate { get; set; }

    /// <summary>Derived from the posting date and the fiscal year variant. Never client-supplied.</summary>
    public byte FiscalPeriod { get; set; }

    public DateTime EntryDateUtc { get; set; }
    public DateOnly? TranslationDate { get; set; }

    public long DocumentCurrencyId { get; set; }
    public long ExchangeRateTypeId { get; set; }
    public decimal ExchangeRateToLocal { get; set; } = 1m;
    public decimal ExchangeRateToGroup { get; set; } = 1m;

    [MaxLength(30)] public string? Reference { get; set; }
    [MaxLength(50)] public string? HeaderText { get; set; }

    public JournalStatus Status { get; set; } = JournalStatus.Draft;
    public SourceModule SourceModule { get; set; } = SourceModule.GeneralLedger;
    [MaxLength(20)] public string? TransactionCode { get; set; }

    public long? ReversalOfDocumentNumber { get; set; }
    public long? ReversedByDocumentNumber { get; set; }
    [MaxLength(2)] public string? ReversalReasonCode { get; set; }

    /// <summary>Links the two documents of an intercompany pair.</summary>
    public Guid? IntercompanyTransactionId { get; set; }

    [MaxLength(64)] public string? IdempotencyKey { get; set; }
    public Guid? CorrelationId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    [MaxLength(64)] public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedAtUtc { get; set; }
    [MaxLength(64)] public string? ModifiedBy { get; set; }
    [MaxLength(64)] public string? PostedBy { get; set; }
    public DateTime? PostedAtUtc { get; set; }

    public byte[]? RowVersion { get; set; }

    public ICollection<JournalEntryLine> Lines { get; set; } = [];
}

/// <summary>
/// The universal journal. Every reporting dimension and every required currency
/// amount lives on the line (ADR-07, ADR-08). Insert-only: no code path and no
/// SQL permission updates a posted row, and clearing state is held in
/// <see cref="OpenItem"/> rather than here (ADR-09).
/// </summary>
public class JournalEntryLine : DomainEntity, ITenantScoped
{
    // Composite key. Lines exist per ledger, so LedgerId is part of it.
    public long TenantId { get; set; }
    public long CompanyCodeId { get; set; }
    public short FiscalYear { get; set; }
    public long DocumentNumber { get; set; }
    public long LedgerId { get; set; }
    public short LineNumber { get; set; }

    public JournalEntryHeader Header { get; set; } = null!;

    // --- Posting -------------------------------------------------------
    [MaxLength(2)] public required string PostingKey { get; set; }
    public DebitCredit DebitCredit { get; set; }
    public AccountType AccountType { get; set; }

    // --- Account and subledger objects ---------------------------------
    public long? GLAccountId { get; set; }
    public long? BusinessPartnerId { get; set; }
    [MaxLength(10)] public string? BusinessPartnerRole { get; set; }
    public long? AssetId { get; set; }
    public int? AssetSubNumber { get; set; }
    [MaxLength(3)] public string? AssetTransactionType { get; set; }

    // --- Controlling dimensions ----------------------------------------
    public long? CostCenterId { get; set; }
    public long? ProfitCenterId { get; set; }
    public long? InternalOrderId { get; set; }
    public long? CostElementId { get; set; }
    public long? ActivityTypeId { get; set; }

    // --- Reporting dimensions ------------------------------------------
    public long? BusinessAreaId { get; set; }
    public long? FunctionalAreaId { get; set; }
    public long? SegmentId { get; set; }
    public long? PlantId { get; set; }
    public long? BranchId { get; set; }

    // --- Intercompany / elimination -------------------------------------
    public long? PartnerCompanyCodeId { get; set; }
    public long? PartnerProfitCenterId { get; set; }
    public long? PartnerSegmentId { get; set; }

    // --- Amounts. Signed: debit positive, credit negative, so a balanced
    //     document sums to zero in every currency. -----------------------
    public decimal DocumentAmount { get; set; }
    public long DocumentCurrencyId { get; set; }

    public decimal LocalAmount { get; set; }
    public long LocalCurrencyId { get; set; }

    public decimal? GroupAmount { get; set; }
    public long? GroupCurrencyId { get; set; }

    public decimal? ControllingAreaAmount { get; set; }
    public long? ControllingAreaCurrencyId { get; set; }

    public decimal? HardCurrencyAmount { get; set; }
    public long? HardCurrencyId { get; set; }

    public decimal? IndexCurrencyAmount { get; set; }
    public long? IndexCurrencyId { get; set; }

    // --- Quantity --------------------------------------------------------
    public decimal? Quantity { get; set; }
    [MaxLength(3)] public string? UnitOfMeasure { get; set; }

    // --- Tax --------------------------------------------------------------
    [MaxLength(2)] public string? TaxCode { get; set; }
    public decimal? TaxBaseAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public bool IsTaxLine { get; set; }

    // --- Payment ----------------------------------------------------------
    public DateOnly? DueDate { get; set; }
    public DateOnly? BaselineDate { get; set; }
    [MaxLength(4)] public string? PaymentTerms { get; set; }
    [MaxLength(1)] public string? PaymentMethod { get; set; }
    [MaxLength(1)] public string? PaymentBlock { get; set; }

    // --- Text --------------------------------------------------------------
    [MaxLength(18)] public string? Assignment { get; set; }
    [MaxLength(50)] public string? LineText { get; set; }
    [MaxLength(30)] public string? Reference1 { get; set; }
    [MaxLength(30)] public string? Reference2 { get; set; }
    [MaxLength(30)] public string? Reference3 { get; set; }

    // --- Provenance ---------------------------------------------------------
    public SourceModule SourceModule { get; set; }
    [MaxLength(2)] public string? SourceDocumentType { get; set; }
    public long? SourceDocumentNumber { get; set; }
    public short? SourceLineNumber { get; set; }
}

/// <summary>
/// Mutable open-item state for an open-item-managed journal line. Separated from
/// the journal so clearing never updates a posted document (ADR-09).
/// </summary>
public class OpenItem : Entity, ITenantScoped, IConcurrencyControlled
{
    public long TenantId { get; set; }

    // Identifies the journal line this item was created from.
    public long CompanyCodeId { get; set; }
    public short FiscalYear { get; set; }
    public long DocumentNumber { get; set; }
    public long LedgerId { get; set; }
    public short LineNumber { get; set; }
    public JournalEntryLine JournalEntryLine { get; set; } = null!;

    public AccountType AccountType { get; set; }
    public long? BusinessPartnerId { get; set; }
    [MaxLength(10)] public string? BusinessPartnerRole { get; set; }
    public long? GLAccountId { get; set; }

    public decimal OriginalAmountDocument { get; set; }
    public decimal OpenAmountDocument { get; set; }
    public long DocumentCurrencyId { get; set; }

    public decimal OriginalAmountLocal { get; set; }
    public decimal OpenAmountLocal { get; set; }
    public long LocalCurrencyId { get; set; }

    public DateOnly? DueDate { get; set; }
    [MaxLength(4)] public string? PaymentTerms { get; set; }

    public ClearingStatus ClearingStatus { get; set; } = ClearingStatus.Open;
    public long? ClearingDocumentNumber { get; set; }
    public DateOnly? ClearingDate { get; set; }

    public byte DunningLevel { get; set; }
    public DateOnly? LastDunningDate { get; set; }

    public byte[]? RowVersion { get; set; }
}

public class ClearingHeader : Entity, ITenantScoped, IAuditable
{
    public long TenantId { get; set; }
    public long CompanyCodeId { get; set; }
    public short FiscalYear { get; set; }
    public long ClearingDocumentNumber { get; set; }
    public DateOnly ClearingDate { get; set; }
    public long CurrencyId { get; set; }

    /// <summary>The journal document posting any payment difference or FX gain/loss.</summary>
    public long? DifferenceDocumentNumber { get; set; }

    public bool IsReset { get; set; }
    public DateTime? ResetAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    [MaxLength(64)] public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedAtUtc { get; set; }
    [MaxLength(64)] public string? ModifiedBy { get; set; }

    public ICollection<ClearingLine> Lines { get; set; } = [];
}

public class ClearingLine : Entity, ITenantScoped
{
    public long TenantId { get; set; }
    public long ClearingHeaderId { get; set; }
    public ClearingHeader ClearingHeader { get; set; } = null!;

    public long OpenItemId { get; set; }
    public OpenItem OpenItem { get; set; } = null!;

    public decimal ClearedAmountDocument { get; set; }
    public decimal ClearedAmountLocal { get; set; }
    public bool IsResidual { get; set; }
}

/// <summary>
/// Deduplicates retried posting requests. A replay returns the original document
/// instead of posting a second one (§22.1).
/// </summary>
public class PostingIdempotency : Entity, ITenantScoped
{
    public long TenantId { get; set; }
    [MaxLength(64)] public required string IdempotencyKey { get; set; }
    public long CompanyCodeId { get; set; }
    public short FiscalYear { get; set; }
    public long DocumentNumber { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
