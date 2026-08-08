using System.ComponentModel.DataAnnotations;
using S4HERP.BuildingBlocks.Domain;
using S4HERP.Organization.Domain;

namespace S4HERP.Workflow.Domain;

public enum WorkflowStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Withdrawn = 4,
}

public enum StepDecision
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Skipped = 4,
}

/// <summary>
/// Which approvals a document needs. Selected by company code, document type and
/// amount, so a 50 USD correction and a 500,000 USD accrual need not travel the
/// same road.
/// </summary>
public class ApprovalRule : AuditableEntity, IDeactivatable, IValidityDated
{
    [MaxLength(20)] public required string Code { get; set; }
    [MaxLength(120)] public required string Name { get; set; }

    /// <summary>
    /// Which kind of object this rule governs — <c>JournalEntry</c>,
    /// <c>PaymentRun</c>. Null matches any, which was the only possible answer
    /// while there was one object type; with two, a journal rule would otherwise
    /// silently start governing payment runs.
    /// </summary>
    [MaxLength(60)] public string? ObjectType { get; set; }

    /// <summary>Null matches any company code.</summary>
    public long? CompanyCodeId { get; set; }
    public CompanyCode? CompanyCode { get; set; }

    /// <summary>Null matches any document type.</summary>
    [MaxLength(2)] public string? DocumentTypeCode { get; set; }

    /// <summary>
    /// Inclusive lower bound on the document's total debit in local currency.
    /// A document matches every rule whose threshold it clears, and each match
    /// becomes a step — so thresholds stack into multi-level approval.
    /// </summary>
    public decimal FromAmount { get; set; }
    public long CurrencyId { get; set; }

    /// <summary>Role a user must hold to decide this step.</summary>
    [MaxLength(40)] public required string ApproverRoleCode { get; set; }

    public int StepSequence { get; set; }

    /// <summary>
    /// When true, the person who created the document cannot approve this step.
    /// Ships on: unreviewed self-approval is the control most audits look for.
    /// </summary>
    public bool MakerCheckerEnforced { get; set; } = true;

    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; } = DateRange.OpenEnded;
    public bool IsActive { get; set; } = true;
}

/// <summary>One approval process for one object.</summary>
public class WorkflowInstance : AuditableEntity
{
    [MaxLength(60)] public required string ObjectType { get; set; }

    /// <summary>Business key of the object, e.g. KSS-1000-2026-SA-0100000001.</summary>
    [MaxLength(120)] public required string ObjectId { get; set; }

    public long CompanyCodeId { get; set; }
    public CompanyCode CompanyCode { get; set; } = null!;

    /// <summary>Who submitted it. Compared against each approver for maker-checker.</summary>
    [MaxLength(64)] public required string SubmittedBy { get; set; }
    public DateTime SubmittedAtUtc { get; set; }

    /// <summary>
    /// Who made the underlying object, which is not always who submitted it.
    /// Both are barred from approving, or an assistant submitting the preparer's
    /// document would launder it straight past maker-checker.
    /// </summary>
    [MaxLength(64)] public required string ObjectCreatedBy { get; set; }

    public WorkflowStatus Status { get; set; } = WorkflowStatus.Pending;
    public DateTime? CompletedAtUtc { get; set; }

    public decimal Amount { get; set; }
    public long CurrencyId { get; set; }

    public ICollection<WorkflowStep> Steps { get; set; } = [];
}

public class WorkflowStep : AuditableEntity, IConcurrencyControlled
{
    public long WorkflowInstanceId { get; set; }
    public WorkflowInstance WorkflowInstance { get; set; } = null!;

    public int Sequence { get; set; }
    [MaxLength(40)] public required string ApproverRoleCode { get; set; }
    public bool MakerCheckerEnforced { get; set; } = true;

    public StepDecision Decision { get; set; } = StepDecision.Pending;
    [MaxLength(64)] public string? DecidedBy { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    [MaxLength(400)] public string? Comment { get; set; }
}
