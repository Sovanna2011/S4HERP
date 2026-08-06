using Microsoft.EntityFrameworkCore;
using S4HERP.BuildingBlocks.Application;
using S4HERP.BuildingBlocks.Infrastructure;
using S4HERP.Finance.Domain;
using S4HERP.Organization.Domain;
using S4HERP.Workflow.Contracts;

namespace S4HERP.Finance.Application;

// ----------------------------------------------------------------- commands

/// <summary>
/// Sends a parked document into approval. The submitter need not be the person
/// who parked it, and both are barred from approving it.
/// </summary>
[RequiresAuthorization("F_BKPF_BUK", "01")]
public sealed record SubmitJournalEntryCommand : ICommand<JournalWorkflowResult>
{
    public required string CompanyCode { get; init; }
    public required short FiscalYear { get; init; }
    public required long DocumentNumber { get; init; }
}

/// <summary>
/// Approves the caller's step. When it is the last step, the document posts:
/// status, open items and the audit entry all happen in the same transaction as
/// the approval, so an approved-but-unposted document cannot exist.
/// </summary>
public sealed record ApproveJournalEntryCommand : ICommand<JournalWorkflowResult>
{
    public required string CompanyCode { get; init; }
    public required short FiscalYear { get; init; }
    public required long DocumentNumber { get; init; }
    public string? Comment { get; init; }
}

/// <summary>Rejects the document. The reason is mandatory and recorded.</summary>
public sealed record RejectJournalEntryCommand : ICommand<JournalWorkflowResult>
{
    public required string CompanyCode { get; init; }
    public required short FiscalYear { get; init; }
    public required long DocumentNumber { get; init; }
    public required string Comment { get; init; }
}

public sealed record JournalWorkflowResult
{
    public required string CompanyCode { get; init; }
    public required short FiscalYear { get; init; }
    public required long DocumentNumber { get; init; }
    public required string DocumentNumberFormatted { get; init; }

    /// <summary>The document's status after the operation.</summary>
    public required string DocumentStatus { get; init; }

    /// <summary>The workflow's outcome: NotRequired, Pending, Approved or Rejected.</summary>
    public required string ApprovalOutcome { get; init; }

    /// <summary>True when this call put the document into the ledger.</summary>
    public required bool Posted { get; init; }

    public int OpenItemsCreated { get; init; }
    public required IReadOnlyList<ApprovalStepView> Steps { get; init; }
}

// ------------------------------------------------------------------ queries

[RequiresAuthorization("F_BKPF_BUK", "03")]
public sealed record GetJournalWorkflowQuery : IQuery<WorkflowStateView>
{
    public required string CompanyCode { get; init; }
    public required short FiscalYear { get; init; }
    public required long DocumentNumber { get; init; }
}

/// <summary>Documents waiting on the calling user. The approver's inbox.</summary>
public sealed record MyJournalApprovalsQuery : IQuery<IReadOnlyList<JournalApprovalInboxItem>>;

public sealed record JournalApprovalInboxItem(
    string CompanyCode,
    string DocumentId,
    decimal Amount,
    string SubmittedBy,
    DateTime SubmittedAtUtc,
    int Sequence,
    string ApproverRoleCode,
    bool MakerCheckerBlocks);

// ----------------------------------------------------------------- handlers

/// <summary>
/// Shared by every path that releases a parked document into the ledger. Kept in
/// one place because "what posting means" must not drift between the
/// no-approval-configured path and the final-approval path.
/// </summary>
public sealed class ParkedDocumentPoster(
    S4herpDbContext db,
    IFiscalPeriodService periods,
    IUserContext user,
    ITenantContext tenant,
    IClock clock)
{
    public async Task<int> PostAsync(
        JournalEntryHeader header, CancellationToken cancellationToken)
    {
        var lines = await db.Set<JournalEntryLine>()
            .Where(l => l.CompanyCodeId == header.CompanyCodeId
                        && l.FiscalYear == header.FiscalYear
                        && l.DocumentNumber == header.DocumentNumber)
            .ToListAsync(cancellationToken);

        if (lines.Count == 0)
        {
            throw new BusinessRuleException(
                PostingErrors.UnknownObject,
                $"Document {header.DocumentNumberFormatted} has no lines to post.");
        }

        // The period is re-checked here, not trusted from park time. Approval can
        // take days, and month-end does not wait for it.
        var period = await periods.DeriveAsync(
            header.CompanyCodeId, header.PostingDate, cancellationToken);
        await periods.RequireOpenAsync(
            header.CompanyCodeId, period,
            lines.Select(l => l.AccountType).Distinct().ToList(),
            cancellationToken);

        header.Status = JournalStatus.Posted;
        // The approver is who posted it. The maker stays in CreatedBy, which is
        // what makes the pair of names evidence that the control was applied.
        header.PostedBy = user.UserName;
        header.PostedAtUtc = clock.UtcNow;

        return await CreateOpenItemsAsync(header, lines, cancellationToken);
    }

    /// <summary>
    /// Recomputed from the lines rather than remembered from park time: the
    /// account's open-item setting at the moment of posting is the one that
    /// governs, and a line already carries every value an open item needs.
    /// </summary>
    private async Task<int> CreateOpenItemsAsync(
        JournalEntryHeader header, List<JournalEntryLine> lines, CancellationToken cancellationToken)
    {
        var glAccountIds = lines
            .Where(l => l.AccountType == AccountType.GeneralLedger)
            .Select(l => l.GLAccountId)
            .OfType<long>()
            .Distinct()
            .ToList();

        var openItemManaged = await db.Set<GLAccountCompanyCode>()
            .Where(s => s.CompanyCodeId == header.CompanyCodeId
                        && glAccountIds.Contains(s.GLAccountId)
                        && s.IsOpenItemManaged)
            .Select(s => s.GLAccountId)
            .ToListAsync(cancellationToken);

        var created = 0;
        foreach (var line in lines)
        {
            var managed = line.AccountType is AccountType.Customer or AccountType.Vendor
                          || (line.GLAccountId is { } id && openItemManaged.Contains(id));

            if (!managed)
            {
                continue;
            }

            db.Add(new OpenItem
            {
                TenantId = tenant.TenantId,
                CompanyCodeId = header.CompanyCodeId,
                FiscalYear = header.FiscalYear,
                DocumentNumber = header.DocumentNumber,
                LedgerId = line.LedgerId,
                LineNumber = line.LineNumber,
                AccountType = line.AccountType,
                BusinessPartnerId = line.BusinessPartnerId,
                BusinessPartnerRole = line.BusinessPartnerRole,
                GLAccountId = line.GLAccountId,
                OriginalAmountDocument = line.DocumentAmount,
                OpenAmountDocument = line.DocumentAmount,
                DocumentCurrencyId = line.DocumentCurrencyId,
                OriginalAmountLocal = line.LocalAmount,
                OpenAmountLocal = line.LocalAmount,
                LocalCurrencyId = line.LocalCurrencyId,
                DueDate = line.DueDate,
                PaymentTerms = line.PaymentTerms,
                ClearingStatus = ClearingStatus.Open,
            });
            created++;
        }

        return created;
    }
}

/// <summary>Loads the document a workflow command names, and refuses politely.</summary>
internal static class JournalWorkflowLookup
{
    public const string ObjectType = "JournalEntry";

    public static async Task<(CompanyCode Company, JournalEntryHeader Header)> LoadAsync(
        S4herpDbContext db, string companyCode, short fiscalYear, long documentNumber,
        CancellationToken cancellationToken)
    {
        var company = await db.Set<CompanyCode>()
            .SingleOrDefaultAsync(c => c.Code == companyCode, cancellationToken)
            ?? throw new NotFoundException($"Company code {companyCode} does not exist.");

        var header = await db.Set<JournalEntryHeader>()
            .Include(h => h.DocumentType)
            .SingleOrDefaultAsync(h => h.CompanyCodeId == company.Id
                                       && h.FiscalYear == fiscalYear
                                       && h.DocumentNumber == documentNumber, cancellationToken)
            ?? throw new NotFoundException(
                $"Document {documentNumber} does not exist in {companyCode} " +
                $"for fiscal year {fiscalYear}.");

        return (company, header);
    }
}

public sealed class SubmitJournalEntryHandler(
    S4herpDbContext db,
    IApprovalService approvals,
    ParkedDocumentPoster poster,
    IAuthorizationEnforcer authorization,
    IUserContext user,
    ITenantContext tenant,
    IClock clock,
    ICorrelationContext correlation)
    : ICommandHandler<SubmitJournalEntryCommand, JournalWorkflowResult>
{
    public async Task<JournalWorkflowResult> HandleAsync(
        SubmitJournalEntryCommand command, CancellationToken cancellationToken)
    {
        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", command.CompanyCode), ("ACTVT", "01")], cancellationToken);

        var (company, header) = await JournalWorkflowLookup.LoadAsync(
            db, command.CompanyCode, command.FiscalYear, command.DocumentNumber, cancellationToken);

        if (header.Status != JournalStatus.Parked)
        {
            throw new BusinessRuleException(
                PostingErrors.NotParked,
                $"Document {header.DocumentNumberFormatted} is {header.Status}. " +
                "Only a parked document can be submitted for approval.");
        }

        var lines = await db.Set<JournalEntryLine>()
            .Where(l => l.CompanyCodeId == company.Id
                        && l.FiscalYear == header.FiscalYear
                        && l.DocumentNumber == header.DocumentNumber)
            .ToListAsync(cancellationToken);

        // Total debit in local currency. The document balances, so debit is the
        // document's value — and local currency is the only measure a threshold
        // configured for a company code can be compared against.
        var amount = lines.Where(l => l.LocalAmount > 0).Sum(l => l.LocalAmount);

        var started = await approvals.StartAsync(new StartApprovalRequest
        {
            ObjectType = JournalWorkflowLookup.ObjectType,
            ObjectId = header.DocumentNumberFormatted,
            CompanyCodeId = company.Id,
            DocumentTypeCode = header.DocumentType.Code,
            Amount = amount,
            CurrencyId = company.LocalCurrencyId,
            ObjectCreatedBy = header.CreatedBy,
        }, cancellationToken);

        var openItems = 0;
        if (started.Outcome == ApprovalOutcome.NotRequired)
        {
            // Nothing is configured to approve a document this size, so there is
            // nobody to wait for. Posting it is the same authority the plain post
            // endpoint would have used; refusing would only strand the document.
            openItems = await poster.PostAsync(header, cancellationToken);
        }
        else
        {
            header.Status = JournalStatus.PendingApproval;
        }

        db.Add(new Audit.Domain.AuditLog
        {
            TenantId = tenant.TenantId,
            OccurredAtUtc = clock.UtcNow,
            UserName = user.UserName,
            CompanyCodeId = company.Id,
            Action = Audit.Domain.AuditAction.Submit,
            ObjectType = JournalWorkflowLookup.ObjectType,
            ObjectId = header.DocumentNumberFormatted,
            SourceApi = "POST /api/v1/finance/journal-entries/{...}/submit",
            TransactionCode = "FV50",
            CorrelationId = correlation.CorrelationId,
            Summary = started.Outcome == ApprovalOutcome.NotRequired
                ? $"Submitted {amount:N2}; no approval rule matched, posted directly."
                : $"Submitted {amount:N2} for approval in {started.Steps.Count} step(s).",
        });

        return new JournalWorkflowResult
        {
            CompanyCode = company.Code,
            FiscalYear = header.FiscalYear,
            DocumentNumber = header.DocumentNumber,
            DocumentNumberFormatted = header.DocumentNumberFormatted,
            DocumentStatus = header.Status.ToString(),
            ApprovalOutcome = started.Outcome.ToString(),
            Posted = header.Status == JournalStatus.Posted,
            OpenItemsCreated = openItems,
            Steps = started.Steps,
        };
    }
}

/// <summary>
/// Approve and reject share everything except the decision, so they share a
/// handler body. Deliberately carries no <c>RequiresAuthorization</c> attribute:
/// the coarse check only ever tests <c>ACTVT</c>, and <c>W_APPROVE</c> is
/// value-carrying on <c>WFTYPE</c> and <c>AMOUNT_TO</c> instead. The real check
/// is below, once the amount is known.
/// </summary>
public abstract class JournalDecisionHandler(
    S4herpDbContext db,
    IApprovalService approvals,
    ParkedDocumentPoster poster,
    IAuthorizationEnforcer authorization,
    IUserContext user,
    ITenantContext tenant,
    IClock clock,
    ICorrelationContext correlation)
{
    protected async Task<JournalWorkflowResult> DecideAsync(
        string companyCode, short fiscalYear, long documentNumber,
        ApprovalDecision decision, string? comment, CancellationToken cancellationToken)
    {
        // Seeing the document is a precondition of deciding on it, and it is also
        // what stops an approver in one company code inspecting another's.
        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", companyCode), ("ACTVT", "03")], cancellationToken);

        var (company, header) = await JournalWorkflowLookup.LoadAsync(
            db, companyCode, fiscalYear, documentNumber, cancellationToken);

        if (header.Status != JournalStatus.PendingApproval)
        {
            throw new BusinessRuleException(
                PostingErrors.NotPendingApproval,
                $"Document {header.DocumentNumberFormatted} is {header.Status} and has no " +
                "approval awaiting a decision.");
        }

        var amount = await db.Set<JournalEntryLine>()
            .Where(l => l.CompanyCodeId == company.Id
                        && l.FiscalYear == header.FiscalYear
                        && l.DocumentNumber == header.DocumentNumber
                        && l.LocalAmount > 0)
            .SumAsync(l => l.LocalAmount, cancellationToken);

        // The approval limit. Zero-padded because the enforcer compares interval
        // bounds as text — see AmountLimit.
        await authorization.RequireAsync(
            "W_APPROVE",
            [("WFTYPE", JournalWorkflowLookup.ObjectType), ("AMOUNT_TO", AmountLimit.Encode(amount))],
            cancellationToken);

        var result = await approvals.DecideAsync(new ApprovalDecisionRequest
        {
            ObjectType = JournalWorkflowLookup.ObjectType,
            ObjectId = header.DocumentNumberFormatted,
            Decision = decision,
            Comment = comment,
        }, cancellationToken);

        var openItems = 0;
        switch (result.Outcome)
        {
            case ApprovalOutcome.Approved:
                openItems = await poster.PostAsync(header, cancellationToken);
                break;
            case ApprovalOutcome.Rejected:
                header.Status = JournalStatus.Rejected;
                break;
        }

        db.Add(new Audit.Domain.AuditLog
        {
            TenantId = tenant.TenantId,
            OccurredAtUtc = clock.UtcNow,
            UserName = user.UserName,
            CompanyCodeId = company.Id,
            Action = decision == ApprovalDecision.Approved
                ? Audit.Domain.AuditAction.Approve
                : Audit.Domain.AuditAction.Reject,
            ObjectType = JournalWorkflowLookup.ObjectType,
            ObjectId = header.DocumentNumberFormatted,
            SourceApi = "POST /api/v1/finance/journal-entries/{...}/"
                        + (decision == ApprovalDecision.Approved ? "approve" : "reject"),
            TransactionCode = "FV50",
            CorrelationId = correlation.CorrelationId,
            Summary = $"Step {result.DecidedStep} {decision.ToString().ToLowerInvariant()}; " +
                      $"workflow {result.Outcome}." +
                      (comment is { Length: > 0 } ? $" Comment: {comment}" : string.Empty),
        });

        return new JournalWorkflowResult
        {
            CompanyCode = company.Code,
            FiscalYear = header.FiscalYear,
            DocumentNumber = header.DocumentNumber,
            DocumentNumberFormatted = header.DocumentNumberFormatted,
            DocumentStatus = header.Status.ToString(),
            ApprovalOutcome = result.Outcome.ToString(),
            Posted = result.Outcome == ApprovalOutcome.Approved,
            OpenItemsCreated = openItems,
            Steps = result.Steps,
        };
    }
}

public sealed class ApproveJournalEntryHandler(
    S4herpDbContext db, IApprovalService approvals, ParkedDocumentPoster poster,
    IAuthorizationEnforcer authorization, IUserContext user, ITenantContext tenant,
    IClock clock, ICorrelationContext correlation)
    : JournalDecisionHandler(db, approvals, poster, authorization, user, tenant, clock, correlation),
        ICommandHandler<ApproveJournalEntryCommand, JournalWorkflowResult>
{
    public Task<JournalWorkflowResult> HandleAsync(
        ApproveJournalEntryCommand command, CancellationToken cancellationToken) =>
        DecideAsync(command.CompanyCode, command.FiscalYear, command.DocumentNumber,
            ApprovalDecision.Approved, command.Comment, cancellationToken);
}

public sealed class RejectJournalEntryHandler(
    S4herpDbContext db, IApprovalService approvals, ParkedDocumentPoster poster,
    IAuthorizationEnforcer authorization, IUserContext user, ITenantContext tenant,
    IClock clock, ICorrelationContext correlation)
    : JournalDecisionHandler(db, approvals, poster, authorization, user, tenant, clock, correlation),
        ICommandHandler<RejectJournalEntryCommand, JournalWorkflowResult>
{
    public Task<JournalWorkflowResult> HandleAsync(
        RejectJournalEntryCommand command, CancellationToken cancellationToken) =>
        DecideAsync(command.CompanyCode, command.FiscalYear, command.DocumentNumber,
            ApprovalDecision.Rejected, command.Comment, cancellationToken);
}

public sealed class GetJournalWorkflowQueryHandler(
    S4herpDbContext db, IApprovalService approvals, IAuthorizationEnforcer authorization)
    : IQueryHandler<GetJournalWorkflowQuery, WorkflowStateView>
{
    public async Task<WorkflowStateView> HandleAsync(
        GetJournalWorkflowQuery query, CancellationToken cancellationToken)
    {
        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", query.CompanyCode), ("ACTVT", "03")], cancellationToken);

        var (_, header) = await JournalWorkflowLookup.LoadAsync(
            db, query.CompanyCode, query.FiscalYear, query.DocumentNumber, cancellationToken);

        return await approvals.GetAsync(
                   JournalWorkflowLookup.ObjectType, header.DocumentNumberFormatted, cancellationToken)
               ?? throw new NotFoundException(
                   $"Document {header.DocumentNumberFormatted} has never been submitted for approval.");
    }
}

public sealed class MyJournalApprovalsQueryHandler(
    S4herpDbContext db, IApprovalService approvals)
    : IQueryHandler<MyJournalApprovalsQuery, IReadOnlyList<JournalApprovalInboxItem>>
{
    public async Task<IReadOnlyList<JournalApprovalInboxItem>> HandleAsync(
        MyJournalApprovalsQuery query, CancellationToken cancellationToken)
    {
        var pending = await approvals.InboxAsync(
            JournalWorkflowLookup.ObjectType, cancellationToken);

        if (pending.Count == 0)
        {
            return [];
        }

        var companyCodeIds = pending.Select(p => p.CompanyCodeId).Distinct().ToList();
        var codes = await db.Set<CompanyCode>()
            .Where(c => companyCodeIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Code, cancellationToken);

        return pending
            .Select(p => new JournalApprovalInboxItem(
                codes.GetValueOrDefault(p.CompanyCodeId, "?"),
                p.ObjectId,
                p.Amount,
                p.SubmittedBy,
                p.SubmittedAtUtc,
                p.Sequence,
                p.ApproverRoleCode,
                p.MakerCheckerBlocks))
            .ToList();
    }
}
