using System.Globalization;

namespace S4HERP.Workflow.Contracts;

/// <summary>
/// The Workflow module's public surface. Other modules call
/// <see cref="IApprovalService"/> and never touch <c>wf</c> tables directly
/// (blueprint 02 §2.2, ADR-02).
///
/// The service is deliberately object-agnostic: it knows a document is worth an
/// amount and was made by somebody, and nothing else. Deciding what to *do* when
/// an approval completes belongs to the module that owns the object — Finance
/// posts a journal entry, and Workflow does not know what posting means.
/// </summary>
public interface IApprovalService
{
    /// <summary>
    /// Matches approval rules and opens a workflow. Returns
    /// <see cref="ApprovalOutcome.NotRequired"/> when no rule matches, which the
    /// caller must treat as "no approval is configured", not as an error.
    /// </summary>
    Task<ApprovalStartResult> StartAsync(
        StartApprovalRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the calling user's decision on the lowest undecided step. Enforces
    /// the approver role and maker-checker; throws rather than returning a flag,
    /// because a refused approval is never a normal outcome.
    /// </summary>
    Task<ApprovalDecisionResult> DecideAsync(
        ApprovalDecisionRequest request, CancellationToken cancellationToken = default);

    /// <summary>The most recent workflow for an object, decided or not.</summary>
    Task<WorkflowStateView?> GetAsync(
        string objectType, string objectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Everything waiting on the calling user: pending steps whose approver role
    /// the caller holds, in a company code the caller is assigned to.
    /// </summary>
    Task<IReadOnlyList<PendingApprovalView>> InboxAsync(
        string? objectType, CancellationToken cancellationToken = default);
}

public enum ApprovalDecision
{
    Approved = 2,
    Rejected = 3,
}

public enum ApprovalOutcome
{
    /// <summary>No approval rule matched. The caller may proceed on its own authority.</summary>
    NotRequired = 1,

    /// <summary>A workflow is open and at least one step is still undecided.</summary>
    Pending = 2,

    /// <summary>Every step approved. The caller should now do whatever approval releases.</summary>
    Approved = 3,

    /// <summary>A step was rejected. Remaining steps are skipped.</summary>
    Rejected = 4,
}

public sealed record StartApprovalRequest
{
    public required string ObjectType { get; init; }
    public required string ObjectId { get; init; }
    public required long CompanyCodeId { get; init; }

    /// <summary>Optional narrowing key, e.g. the accounting document type.</summary>
    public string? DocumentTypeCode { get; init; }

    /// <summary>Value the thresholds are compared against, in <see cref="CurrencyId"/>.</summary>
    public required decimal Amount { get; init; }
    public required long CurrencyId { get; init; }

    /// <summary>
    /// Who made the object, which is not always who submits it. Both are barred
    /// from approving where maker-checker is enforced — an assistant submitting
    /// the preparer's document must not launder it past the control.
    /// </summary>
    public required string ObjectCreatedBy { get; init; }
}

public sealed record ApprovalStartResult
{
    public required ApprovalOutcome Outcome { get; init; }
    public required IReadOnlyList<ApprovalStepView> Steps { get; init; }
}

public sealed record ApprovalDecisionRequest
{
    public required string ObjectType { get; init; }
    public required string ObjectId { get; init; }
    public required ApprovalDecision Decision { get; init; }
    public string? Comment { get; init; }
}

public sealed record ApprovalDecisionResult
{
    public required ApprovalOutcome Outcome { get; init; }
    public required int DecidedStep { get; init; }
    public required int RemainingSteps { get; init; }
    public required IReadOnlyList<ApprovalStepView> Steps { get; init; }
}

public sealed record ApprovalStepView(
    int Sequence,
    string ApproverRoleCode,
    bool MakerCheckerEnforced,
    string Decision,
    string? DecidedBy,
    DateTime? DecidedAtUtc,
    string? Comment);

public sealed record WorkflowStateView
{
    public required string ObjectType { get; init; }
    public required string ObjectId { get; init; }
    public required string Status { get; init; }
    public required string SubmittedBy { get; init; }
    public required DateTime SubmittedAtUtc { get; init; }
    public required string ObjectCreatedBy { get; init; }
    public required decimal Amount { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public required IReadOnlyList<ApprovalStepView> Steps { get; init; }
}

public sealed record PendingApprovalView(
    string ObjectType,
    string ObjectId,
    long CompanyCodeId,
    decimal Amount,
    string SubmittedBy,
    DateTime SubmittedAtUtc,
    int Sequence,
    string ApproverRoleCode,
    bool MakerCheckerBlocks);

/// <summary>Stable error codes, so a client can branch on the reason rather than the wording.</summary>
public static class ApprovalErrors
{
    public const string MakerChecker = "MAKER_CHECKER_VIOLATION";
    public const string NotAnApprover = "NOT_AN_APPROVER";
    public const string NoPendingWorkflow = "NO_PENDING_WORKFLOW";
    public const string AlreadyInApproval = "ALREADY_IN_APPROVAL";
    public const string CommentRequired = "REJECTION_COMMENT_REQUIRED";
}

/// <summary>
/// Encoding for amount-limited authorisation fields such as
/// <c>W_APPROVE.AMOUNT_TO</c>.
/// </summary>
public static class AmountLimit
{
    /// <summary>
    /// The authorisation enforcer compares interval bounds ordinally, because an
    /// authorisation field value is text and most of them — company code, document
    /// type — are not numbers. A numeric limit therefore has to be encoded so that
    /// text order *is* numeric order: fixed width, zero-padded. Without it "9"
    /// sorts above "10" and a 10,000 approval limit would pass a 90,000 document.
    ///
    /// Fifteen integer digits and four decimals is exactly the range of the
    /// <c>decimal(19,4)</c> that every amount in this ledger is stored as.
    /// </summary>
    public const string Format = "000000000000000.0000";

    public static string Encode(decimal amount) =>
        Math.Abs(amount).ToString(Format, CultureInfo.InvariantCulture);
}
