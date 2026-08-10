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

/// <summary>
/// What the bank said about one instruction, from an ISO 20022 pain.002 payment
/// status report.
///
/// The vocabulary is the standard's (<c>TxSts</c>), not ours, because
/// translating it would lose the distinction the bank was making. <c>ACCP</c>
/// and <c>ACSC</c> both mean the transfer is going ahead, but only the second
/// says the money has actually settled, and a treasurer chasing a payment needs
/// to know which one they were told.
/// </summary>
public enum BankTransactionStatus
{
    /// <summary>ACTC/ACCP — technically valid and accepted. Not yet settled.</summary>
    Accepted = 1,

    /// <summary>ACSC — settled. The money has left.</summary>
    Settled = 2,

    /// <summary>PDNG — the bank is still deciding, typically awaiting funds or a check.</summary>
    Pending = 3,

    /// <summary>RJCT — refused. The money did not move, and the ledger still says it did.</summary>
    Rejected = 4,

    /// <summary>
    /// A status code the parser does not recognise. Recorded rather than
    /// rejected: an unknown code from a real bank is information, and discarding
    /// the row because of it would lose the payment it referred to.
    /// </summary>
    Unknown = 9,
}

/// <summary>
/// One imported pain.002. Stored whole for the same reason the outgoing file is:
/// when a payment is disputed, what the bank actually sent is the evidence, and
/// a summary reconstructed from parsed rows is not it.
/// </summary>
public class PaymentStatusReport : AuditableEntity
{
    /// <summary>The report's own <c>GrpHdr/MsgId</c>. Unique: re-importing is a no-op, not a duplicate.</summary>
    [MaxLength(35)] public required string MessageId { get; set; }

    /// <summary>
    /// <c>OrgnlGrpInfAndSts/OrgnlMsgId</c> — the pain.001 this answers. A report
    /// naming a message we never sent is refused, because the alternative is
    /// silently filing somebody else's bank traffic against our payments.
    /// </summary>
    [MaxLength(35)] public required string OriginalMessageId { get; set; }

    public long PaymentFileId { get; set; }
    public PaymentFile PaymentFile { get; set; } = null!;

    [MaxLength(30)] public required string Format { get; set; }

    /// <summary>Group-level status, where the bank gave one. Individual items may still differ.</summary>
    public BankTransactionStatus? GroupStatus { get; set; }

    public required string Content { get; set; }
    [MaxLength(64)] public required string ContentSha256 { get; set; }

    public DateTime? ReportCreatedAtUtc { get; set; }
    public DateTime ImportedAtUtc { get; set; }
    [MaxLength(64)] public required string ImportedBy { get; set; }

    public ICollection<PaymentStatusItem> Items { get; set; } = [];
}

/// <summary>
/// The bank's verdict on one payment. Matched to the payment document through
/// <c>OrgnlEndToEndId</c>, which is why the outgoing file sets that to something
/// traceable rather than a random identifier (increment 8).
/// </summary>
public class PaymentStatusItem : AuditableEntity
{
    public long PaymentStatusReportId { get; set; }
    public PaymentStatusReport PaymentStatusReport { get; set; } = null!;

    [MaxLength(35)] public required string EndToEndId { get; set; }

    public BankTransactionStatus Status { get; set; }

    /// <summary>ISO 20022 <c>StsRsnInf/Rsn/Cd</c>, e.g. AC01 for an invalid account number.</summary>
    [MaxLength(4)] public string? ReasonCode { get; set; }

    /// <summary>The bank's own words. Kept verbatim; a paraphrase is not evidence.</summary>
    [MaxLength(400)] public string? ReasonText { get; set; }

    public decimal? Amount { get; set; }

    /// <summary>
    /// The payment document this refers to, when the end-to-end id resolved to
    /// one. Null means the bank reported a transaction we cannot place — which is
    /// recorded, and is exactly the sort of thing a person must look at.
    /// </summary>
    public long? PaymentDocumentNumber { get; set; }
    public short? FiscalYear { get; set; }
    public long? CompanyCodeId { get; set; }
    public CompanyCode? CompanyCode { get; set; }

    /// <summary>
    /// Set when a rejection has been acted on — the payment reversed and its
    /// invoices reopened. Until then the rejection is outstanding and the ledger
    /// disagrees with the bank.
    /// </summary>
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    [MaxLength(64)] public string? ResolvedBy { get; set; }
    public long? ReversalDocumentNumber { get; set; }
}

/// <summary>Where a statement line stands against the ledger.</summary>
public enum StatementLineStatus
{
    /// <summary>Nothing in the ledger has been identified as this line.</summary>
    Unmatched = 1,

    /// <summary>Tied to a posted document. The bank and the books agree about this movement.</summary>
    Matched = 2,

    /// <summary>
    /// Deliberately set aside — bank charges, interest, a transfer between our
    /// own accounts. Not matched and not outstanding: somebody has looked and
    /// decided it needs no document of ours.
    /// </summary>
    Ignored = 3,
}

/// <summary>
/// One imported ISO 20022 camt.053 bank statement.
///
/// This is the first thing in the system that describes what the bank account
/// actually did, as opposed to what this system instructed or what the bank said
/// about those instructions. A payment can be accepted in a pain.002 and still
/// come back a week later; only the statement shows that.
/// </summary>
public class BankStatement : AuditableEntity
{
    /// <summary>camt.053 <c>Stmt/Id</c>. Unique per tenant: re-importing is a no-op.</summary>
    [MaxLength(35)] public required string StatementId { get; set; }

    /// <summary>Statement sequence within the account, where the bank gives one.</summary>
    public int? LegalSequenceNumber { get; set; }

    public long HouseBankAccountId { get; set; }
    public HouseBankAccount HouseBankAccount { get; set; } = null!;

    public long CompanyCodeId { get; set; }
    public CompanyCode CompanyCode { get; set; } = null!;

    [MaxLength(30)] public required string Format { get; set; }

    /// <summary>
    /// The bank's own balances. Stored rather than derived, because the point of
    /// a statement is that it is the bank's assertion — recomputing it from the
    /// lines would replace the assertion with our arithmetic and lose the ability
    /// to notice that they disagree.
    /// </summary>
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public long CurrencyId { get; set; }

    public DateOnly StatementDate { get; set; }
    public DateOnly? FromDate { get; set; }

    public required string Content { get; set; }
    [MaxLength(64)] public required string ContentSha256 { get; set; }

    public DateTime ImportedAtUtc { get; set; }
    [MaxLength(64)] public required string ImportedBy { get; set; }

    public ICollection<BankStatementLine> Lines { get; set; } = [];
}

/// <summary>
/// One movement on the account, as the bank recorded it.
///
/// The line is never edited to match the ledger. Reconciliation records the
/// correspondence between a bank movement and a posted document; it does not
/// adjust either. A statement that has been "corrected" is no longer evidence.
/// </summary>
public class BankStatementLine : AuditableEntity, IConcurrencyControlled
{
    public long BankStatementId { get; set; }
    public BankStatement BankStatement { get; set; } = null!;

    /// <summary>Position within the statement, so the order the bank sent survives.</summary>
    public int LineNumber { get; set; }

    /// <summary>The bank's reference for the entry, where it gave one.</summary>
    [MaxLength(35)] public string? EntryReference { get; set; }

    /// <summary>Always positive; direction is in <see cref="IsCredit"/>.</summary>
    public decimal Amount { get; set; }

    /// <summary>True for money in, false for money out. The bank's CdtDbtInd.</summary>
    public bool IsCredit { get; set; }

    public DateOnly? BookingDate { get; set; }
    public DateOnly? ValueDate { get; set; }

    /// <summary>
    /// <c>EndToEndId</c> from the entry's transaction details, which is what
    /// makes automatic matching possible at all — it is the identifier this
    /// system put on the outgoing instruction in increment 8.
    /// </summary>
    [MaxLength(35)] public string? EndToEndId { get; set; }

    /// <summary>Unstructured remittance information, verbatim.</summary>
    [MaxLength(400)] public string? RemittanceInformation { get; set; }

    /// <summary>Whoever the bank says was on the other side.</summary>
    [MaxLength(140)] public string? CounterpartyName { get; set; }

    /// <summary>ISO 20022 bank transaction code, e.g. PMNT/RCDT/ESCT.</summary>
    [MaxLength(35)] public string? BankTransactionCode { get; set; }

    public StatementLineStatus Status { get; set; } = StatementLineStatus.Unmatched;

    public short? FiscalYear { get; set; }
    public long? DocumentNumber { get; set; }

    /// <summary>How the correspondence was established, so a reviewer can weigh it.</summary>
    [MaxLength(30)] public string? MatchMethod { get; set; }

    [MaxLength(64)] public string? MatchedBy { get; set; }
    public DateTime? MatchedAtUtc { get; set; }
    [MaxLength(200)] public string? MatchComment { get; set; }
}
