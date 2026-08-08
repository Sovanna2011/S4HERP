using System.ComponentModel.DataAnnotations;
using S4HERP.BuildingBlocks.Domain;
using S4HERP.Organization.Domain;

namespace S4HERP.Finance.Domain;

/// <summary>
/// A bank the company banks with, per company code (SAP's T012). In <c>fin</c>
/// rather than <c>cfg</c> because an account here points at a G/L account, and
/// Finance owns those — Organization depends on nothing.
/// </summary>
public class HouseBank : AuditableEntity, IDeactivatable
{
    [MaxLength(5)] public required string Code { get; set; }
    [MaxLength(60)] public required string Name { get; set; }

    public long CompanyCodeId { get; set; }
    public CompanyCode CompanyCode { get; set; } = null!;

    [MaxLength(2)] public required string CountryCode { get; set; }
    [MaxLength(11)] public string? SwiftCode { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<HouseBankAccount> Accounts { get; set; } = [];
}

/// <summary>
/// One account at a house bank, and the G/L account it reconciles to. A payment
/// run pays *from* one of these; the G/L account is the bank side of every
/// document it posts.
/// </summary>
public class HouseBankAccount : AuditableEntity, IDeactivatable
{
    public long HouseBankId { get; set; }
    public HouseBank HouseBank { get; set; } = null!;

    [MaxLength(5)] public required string Code { get; set; }
    [MaxLength(35)] public required string AccountNumber { get; set; }
    [MaxLength(34)] public string? Iban { get; set; }

    public long CurrencyId { get; set; }

    /// <summary>The bank G/L account. Never a reconciliation account.</summary>
    public long GLAccountId { get; set; }
    public GLAccount GLAccount { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}

public enum PaymentRunStatus
{
    Proposed = 1,
    Executed = 2,
    Deleted = 3,
    PendingApproval = 4,
    Approved = 5,
    Rejected = 6,
}

/// <summary>
/// F110. A payment run proposes before it pays: the point of the proposal is
/// that somebody can look at what is about to leave the bank, and at what was
/// deliberately left out and why, before any of it is posted.
/// </summary>
public class PaymentRun : AuditableEntity
{
    /// <summary>Human-facing identifier, e.g. <c>1000-2026-04-20-A</c>.</summary>
    [MaxLength(40)] public required string RunId { get; set; }

    public long CompanyCodeId { get; set; }
    public CompanyCode CompanyCode { get; set; } = null!;

    /// <summary>Posting date of the payments this run will make.</summary>
    public DateOnly RunDate { get; set; }

    /// <summary>Items due on or before this date are candidates.</summary>
    public DateOnly DueBy { get; set; }

    [MaxLength(1)] public required string PaymentMethodCode { get; set; }

    public long HouseBankAccountId { get; set; }
    public HouseBankAccount HouseBankAccount { get; set; } = null!;

    public PaymentRunStatus Status { get; set; } = PaymentRunStatus.Proposed;
    public DateTime? ExecutedAtUtc { get; set; }
    [MaxLength(64)] public string? ExecutedBy { get; set; }

    public ICollection<PaymentRunItem> Items { get; set; } = [];
}

/// <summary>
/// One candidate open item. Excluded candidates are kept, with the reason —
/// a proposal that silently omits an invoice is how a supplier goes unpaid for
/// a month and nobody can say why.
/// </summary>
public class PaymentRunItem : AuditableEntity
{
    public long PaymentRunId { get; set; }
    public PaymentRun PaymentRun { get; set; } = null!;

    public long OpenItemId { get; set; }
    public OpenItem OpenItem { get; set; } = null!;

    public long BusinessPartnerId { get; set; }
    [MaxLength(20)] public required string BusinessPartnerNumber { get; set; }

    public short FiscalYear { get; set; }
    public long DocumentNumber { get; set; }
    public short LineNumber { get; set; }
    public DateOnly? DueDate { get; set; }

    /// <summary>Magnitude to be paid, in the item's document currency.</summary>
    public decimal Amount { get; set; }
    public long CurrencyId { get; set; }

    public bool IsExcluded { get; set; }
    [MaxLength(160)] public string? ExclusionReason { get; set; }

    /// <summary>Set when the run is executed and this item is actually paid.</summary>
    public long? PaymentDocumentNumber { get; set; }
}

/// <summary>
/// The instruction sent to the bank for an executed run: an ISO 20022
/// customer credit transfer initiation (pain.001).
///
/// Generated once and never again. A payment file is not a report — a second
/// copy carrying a new message identifier is, to the bank, a second instruction,
/// and the money leaves twice. Re-download the stored one instead; that is what
/// <see cref="ContentSha256"/> and the download counters are for.
///
/// The XML holds full account numbers, so it is the one place in the system
/// where they appear in the clear. Reading it needs S_EXPORT and every read is
/// audited; nothing else in the API returns an unmasked account.
/// </summary>
public class PaymentFile : AuditableEntity
{
    public long PaymentRunId { get; set; }
    public PaymentRun PaymentRun { get; set; } = null!;

    /// <summary>ISO 20022 <c>GrpHdr/MsgId</c>. The bank rejects a duplicate.</summary>
    [MaxLength(35)] public required string MessageId { get; set; }

    [MaxLength(30)] public required string Format { get; set; }

    public required string Content { get; set; }

    /// <summary>
    /// Hex SHA-256 of <see cref="Content"/>. Lets a treasurer prove the file the
    /// bank received is the file this run generated, which is the question asked
    /// after a disputed payment.
    /// </summary>
    [MaxLength(64)] public required string ContentSha256 { get; set; }

    public int TransactionCount { get; set; }

    /// <summary>ISO 20022 <c>CtrlSum</c>: what the bank must total to.</summary>
    public decimal ControlSum { get; set; }
    public long CurrencyId { get; set; }

    public DateTime GeneratedAtUtc { get; set; }
    [MaxLength(64)] public required string GeneratedBy { get; set; }

    public int DownloadCount { get; set; }
    public DateTime? FirstDownloadedAtUtc { get; set; }
    [MaxLength(64)] public string? FirstDownloadedBy { get; set; }
    public DateTime? LastDownloadedAtUtc { get; set; }
    [MaxLength(64)] public string? LastDownloadedBy { get; set; }
}
