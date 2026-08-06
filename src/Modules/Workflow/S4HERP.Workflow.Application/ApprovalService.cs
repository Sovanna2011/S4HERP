using Microsoft.EntityFrameworkCore;
using S4HERP.BuildingBlocks.Application;
using S4HERP.BuildingBlocks.Infrastructure;
using S4HERP.Workflow.Contracts;
using S4HERP.Workflow.Domain;

namespace S4HERP.Workflow.Application;

/// <summary>
/// The approval engine. Owns <c>wf</c> and nothing else: it decides who may
/// approve and records that they did, and never touches the object under
/// approval. Whatever approval *releases* is the owning module's business, which
/// is what keeps Finance → Workflow a one-way dependency.
/// </summary>
public sealed class ApprovalService(
    S4herpDbContext db,
    IAuthorizationEnforcer authorization,
    IUserContext user,
    ITenantContext tenant,
    IClock clock) : IApprovalService
{
    public async Task<ApprovalStartResult> StartAsync(
        StartApprovalRequest request, CancellationToken cancellationToken = default)
    {
        var open = await db.Set<WorkflowInstance>()
            .AnyAsync(w => w.ObjectType == request.ObjectType
                           && w.ObjectId == request.ObjectId
                           && w.Status == WorkflowStatus.Pending, cancellationToken);

        if (open)
        {
            throw new BusinessRuleException(
                ApprovalErrors.AlreadyInApproval,
                $"{request.ObjectType} {request.ObjectId} is already awaiting approval.");
        }

        var steps = await MatchRulesAsync(request, cancellationToken);

        if (steps.Count == 0)
        {
            // Configuration says this document needs nobody's signature. Saying so
            // is the honest answer; inventing an approver would be worse.
            return new ApprovalStartResult { Outcome = ApprovalOutcome.NotRequired, Steps = [] };
        }

        var instance = new WorkflowInstance
        {
            TenantId = tenant.TenantId,
            ObjectType = request.ObjectType,
            ObjectId = request.ObjectId,
            CompanyCodeId = request.CompanyCodeId,
            SubmittedBy = user.UserName,
            SubmittedAtUtc = clock.UtcNow,
            ObjectCreatedBy = request.ObjectCreatedBy,
            Status = WorkflowStatus.Pending,
            Amount = request.Amount,
            CurrencyId = request.CurrencyId,
        };

        var sequence = 0;
        foreach (var rule in steps)
        {
            instance.Steps.Add(new WorkflowStep
            {
                TenantId = tenant.TenantId,
                // Renumbered densely. Rules may be configured 10, 20, 30 to leave
                // room for insertion, and "step 2 of 3" is what a person needs.
                Sequence = ++sequence,
                ApproverRoleCode = rule.ApproverRoleCode,
                MakerCheckerEnforced = rule.MakerCheckerEnforced,
                Decision = StepDecision.Pending,
            });
        }

        db.Add(instance);

        return new ApprovalStartResult
        {
            Outcome = ApprovalOutcome.Pending,
            Steps = instance.Steps.OrderBy(s => s.Sequence).Select(ToView).ToList(),
        };
    }

    public async Task<ApprovalDecisionResult> DecideAsync(
        ApprovalDecisionRequest request, CancellationToken cancellationToken = default)
    {
        var instance = await db.Set<WorkflowInstance>()
            .Include(w => w.Steps)
            .SingleOrDefaultAsync(w => w.ObjectType == request.ObjectType
                                       && w.ObjectId == request.ObjectId
                                       && w.Status == WorkflowStatus.Pending, cancellationToken)
            ?? throw new BusinessRuleException(
                ApprovalErrors.NoPendingWorkflow,
                $"{request.ObjectType} {request.ObjectId} has no approval awaiting a decision.");

        var step = instance.Steps
            .Where(s => s.Decision == StepDecision.Pending)
            .OrderBy(s => s.Sequence)
            .FirstOrDefault()
            ?? throw new BusinessRuleException(
                ApprovalErrors.NoPendingWorkflow,
                $"{request.ObjectType} {request.ObjectId} has no undecided step.");

        // Maker-checker before the role check: someone who both made the document
        // and holds the approver role is the exact case the control exists for, and
        // naming that reason is more useful than "you are not an approver".
        if (step.MakerCheckerEnforced && IsOwnWork(instance))
        {
            throw new BusinessRuleException(
                ApprovalErrors.MakerChecker,
                $"You submitted or created {request.ObjectType} {request.ObjectId} and cannot " +
                "decide its approval. Maker-checker is enforced on this step.");
        }

        var roles = await authorization.RoleCodesAsync(cancellationToken);
        if (!roles.Contains(step.ApproverRoleCode, StringComparer.OrdinalIgnoreCase))
        {
            throw new AuthorizationException(
                $"Step {step.Sequence} is decided by role {step.ApproverRoleCode}, which you do not hold.");
        }

        if (request.Decision == ApprovalDecision.Rejected
            && string.IsNullOrWhiteSpace(request.Comment))
        {
            // An unexplained rejection tells the preparer nothing and tells an
            // auditor less.
            throw new BusinessRuleException(
                ApprovalErrors.CommentRequired, "A rejection must state a reason.");
        }

        step.Decision = request.Decision == ApprovalDecision.Approved
            ? StepDecision.Approved
            : StepDecision.Rejected;
        step.DecidedBy = user.UserName;
        step.DecidedAtUtc = clock.UtcNow;
        step.Comment = request.Comment;

        ApprovalOutcome outcome;
        if (step.Decision == StepDecision.Rejected)
        {
            foreach (var remaining in instance.Steps.Where(s => s.Decision == StepDecision.Pending))
            {
                remaining.Decision = StepDecision.Skipped;
                remaining.DecidedBy = user.UserName;
                remaining.DecidedAtUtc = clock.UtcNow;
                remaining.Comment = $"Skipped: rejected at step {step.Sequence}.";
            }

            instance.Status = WorkflowStatus.Rejected;
            instance.CompletedAtUtc = clock.UtcNow;
            outcome = ApprovalOutcome.Rejected;
        }
        else if (instance.Steps.Any(s => s.Decision == StepDecision.Pending))
        {
            outcome = ApprovalOutcome.Pending;
        }
        else
        {
            instance.Status = WorkflowStatus.Approved;
            instance.CompletedAtUtc = clock.UtcNow;
            outcome = ApprovalOutcome.Approved;
        }

        return new ApprovalDecisionResult
        {
            Outcome = outcome,
            DecidedStep = step.Sequence,
            RemainingSteps = instance.Steps.Count(s => s.Decision == StepDecision.Pending),
            Steps = instance.Steps.OrderBy(s => s.Sequence).Select(ToView).ToList(),
        };
    }

    public async Task<ApprovalDecisionResult> WithdrawAsync(
        string objectType, string objectId, string? comment,
        CancellationToken cancellationToken = default)
    {
        var instance = await db.Set<WorkflowInstance>()
            .Include(w => w.Steps)
            .SingleOrDefaultAsync(w => w.ObjectType == objectType
                                       && w.ObjectId == objectId
                                       && w.Status == WorkflowStatus.Pending, cancellationToken)
            ?? throw new BusinessRuleException(
                ApprovalErrors.NoPendingWorkflow,
                $"{objectType} {objectId} has no pending approval to withdraw.");

        // Only the people the approvers are checking. An approver who could
        // withdraw rather than reject would erase the evidence that they saw it.
        if (!IsOwnWork(instance))
        {
            throw new AuthorizationException(
                $"{objectType} {objectId} was submitted by {instance.SubmittedBy}. " +
                "Only the submitter or the person who created it may withdraw it.");
        }

        // Once somebody has approved a step, withdrawal would quietly undo a
        // decision that was recorded. Reject it instead, on the record.
        if (instance.Steps.Any(s => s.Decision == StepDecision.Approved))
        {
            throw new BusinessRuleException(
                ApprovalErrors.AlreadyDecided,
                $"{objectType} {objectId} has already been approved at one or more steps " +
                "and can no longer be withdrawn.");
        }

        foreach (var step in instance.Steps.Where(s => s.Decision == StepDecision.Pending))
        {
            step.Decision = StepDecision.Skipped;
            step.DecidedBy = user.UserName;
            step.DecidedAtUtc = clock.UtcNow;
            step.Comment = comment is { Length: > 0 }
                ? $"Withdrawn by the submitter: {comment}"
                : "Withdrawn by the submitter.";
        }

        instance.Status = WorkflowStatus.Withdrawn;
        instance.CompletedAtUtc = clock.UtcNow;

        return new ApprovalDecisionResult
        {
            Outcome = ApprovalOutcome.Withdrawn,
            DecidedStep = 0,
            RemainingSteps = 0,
            Steps = instance.Steps.OrderBy(s => s.Sequence).Select(ToView).ToList(),
        };
    }

    public async Task<WorkflowStateView?> GetAsync(
        string objectType, string objectId, CancellationToken cancellationToken = default)
    {
        var instance = await db.Set<WorkflowInstance>()
            .AsNoTracking()
            .Include(w => w.Steps)
            .Where(w => w.ObjectType == objectType && w.ObjectId == objectId)
            .OrderByDescending(w => w.SubmittedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (instance is null)
        {
            return null;
        }

        return new WorkflowStateView
        {
            ObjectType = instance.ObjectType,
            ObjectId = instance.ObjectId,
            Status = instance.Status.ToString(),
            SubmittedBy = instance.SubmittedBy,
            SubmittedAtUtc = instance.SubmittedAtUtc,
            ObjectCreatedBy = instance.ObjectCreatedBy,
            Amount = instance.Amount,
            CompletedAtUtc = instance.CompletedAtUtc,
            Steps = instance.Steps.OrderBy(s => s.Sequence).Select(ToView).ToList(),
        };
    }

    public async Task<IReadOnlyList<PendingApprovalView>> InboxAsync(
        string? objectType, CancellationToken cancellationToken = default)
    {
        var roles = await authorization.RoleCodesAsync(cancellationToken);
        if (roles.Count == 0)
        {
            return [];
        }

        var companyCodes = await authorization.AuthorizedCompanyCodeIdsAsync(cancellationToken);

        var pending = await db.Set<WorkflowInstance>()
            .AsNoTracking()
            .Include(w => w.Steps)
            .Where(w => w.Status == WorkflowStatus.Pending
                        && (objectType == null || w.ObjectType == objectType)
                        && companyCodes.Contains(w.CompanyCodeId))
            .OrderBy(w => w.SubmittedAtUtc)
            .ToListAsync(cancellationToken);

        return pending
            .Select(w => new
            {
                Instance = w,
                Step = w.Steps.Where(s => s.Decision == StepDecision.Pending)
                    .OrderBy(s => s.Sequence).FirstOrDefault(),
            })
            .Where(x => x.Step is not null
                        && roles.Contains(x.Step!.ApproverRoleCode, StringComparer.OrdinalIgnoreCase))
            .Select(x => new PendingApprovalView(
                x.Instance.ObjectType,
                x.Instance.ObjectId,
                x.Instance.CompanyCodeId,
                x.Instance.Amount,
                x.Instance.SubmittedBy,
                x.Instance.SubmittedAtUtc,
                x.Step!.Sequence,
                x.Step.ApproverRoleCode,
                // Shown rather than hidden: an approver needs to see that a document
                // is waiting and know why they personally cannot release it.
                x.Step.MakerCheckerEnforced && IsOwnWork(x.Instance)))
            .ToList();
    }

    /// <summary>
    /// Rules whose thresholds the amount clears, one step per configured sequence.
    /// Where two rules occupy the same sequence, the higher threshold wins — it is
    /// the more specific statement about a document of this size.
    /// </summary>
    private async Task<List<ApprovalRule>> MatchRulesAsync(
        StartApprovalRequest request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var amount = Math.Abs(request.Amount);

        var matches = await db.Set<ApprovalRule>()
            .AsNoTracking()
            .Where(r => r.IsActive
                        && r.ValidFrom <= today && r.ValidTo > today
                        && (r.CompanyCodeId == null || r.CompanyCodeId == request.CompanyCodeId)
                        && (r.DocumentTypeCode == null
                            || r.DocumentTypeCode == request.DocumentTypeCode)
                        && r.CurrencyId == request.CurrencyId
                        && r.FromAmount <= amount)
            .ToListAsync(cancellationToken);

        return matches
            .GroupBy(r => r.StepSequence)
            .Select(g => g.OrderByDescending(r => r.FromAmount).ThenBy(r => r.Code).First())
            .OrderBy(r => r.StepSequence)
            .ToList();
    }

    private bool IsOwnWork(WorkflowInstance instance) =>
        string.Equals(instance.SubmittedBy, user.UserName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(instance.ObjectCreatedBy, user.UserName, StringComparison.OrdinalIgnoreCase);

    private static ApprovalStepView ToView(WorkflowStep s) => new(
        s.Sequence, s.ApproverRoleCode, s.MakerCheckerEnforced,
        s.Decision.ToString(), s.DecidedBy, s.DecidedAtUtc, s.Comment);
}
