using Microsoft.EntityFrameworkCore;
using S4HERP.BuildingBlocks.Application;
using S4HERP.BuildingBlocks.Infrastructure;
using S4HERP.Finance.Domain;
using S4HERP.Organization.Domain;
using S4HERP.Workflow.Contracts;

namespace S4HERP.Finance.Application;

// ----------------------------------------------------------------- commands

/// <summary>
/// F110, proposal step. Selects what could be paid and records what could not,
/// with the reason. Nothing is posted: the whole point of a proposal is that
/// somebody sees the money before it leaves.
/// </summary>
[RequiresAuthorization("F_BKPF_BUK", "01")]
public sealed record CreatePaymentProposalCommand : ICommand<PaymentProposalResult>
{
    public required string CompanyCode { get; init; }

    /// <summary>Posting date of the payments this run will make.</summary>
    public required DateOnly RunDate { get; init; }

    /// <summary>Items due on or before this date are candidates.</summary>
    public required DateOnly DueBy { get; init; }

    public required string PaymentMethod { get; init; }

    /// <summary>House bank code and account code the money moves from.</summary>
    public required string HouseBank { get; init; }
    public required string HouseBankAccount { get; init; }

    /// <summary>Optional: restrict the run to one partner.</summary>
    public string? BusinessPartner { get; init; }
}

/// <summary>
/// Sends a proposal for approval. A payment run is where maker-checker earns its
/// keep more than anywhere else in the system: it is the one transaction that
/// moves money out of the company in bulk.
/// </summary>
[RequiresAuthorization("F_BKPF_BUK", "01")]
public sealed record SubmitPaymentRunCommand : ICommand<PaymentRunApprovalResult>
{
    public required string RunId { get; init; }
}

/// <summary>Approves the caller's step. The last one releases the run for execution.</summary>
public sealed record ApprovePaymentRunCommand : ICommand<PaymentRunApprovalResult>
{
    public required string RunId { get; init; }
    public string? Comment { get; init; }
}

/// <summary>Rejects the run. The reason is mandatory.</summary>
public sealed record RejectPaymentRunCommand : ICommand<PaymentRunApprovalResult>
{
    public required string RunId { get; init; }
    public required string Comment { get; init; }
}

public sealed record PaymentRunApprovalResult
{
    public required string RunId { get; init; }
    public required string Status { get; init; }
    public required string ApprovalOutcome { get; init; }
    public required decimal TotalToPay { get; init; }
    public required IReadOnlyList<ApprovalStepView> Steps { get; init; }
}

/// <summary>Posts the proposal. One payment document per partner.</summary>
[RequiresAuthorization("F_BKPF_BUK", "01")]
public sealed record ExecutePaymentRunCommand : ICommand<PaymentRunExecutionResult>
{
    public required string RunId { get; init; }
}

/// <summary>Discards a proposal that was never executed.</summary>
[RequiresAuthorization("F_BKPF_BUK", "06")]
public sealed record DeletePaymentProposalCommand : ICommand<PaymentRunExecutionResult>
{
    public required string RunId { get; init; }
}

[RequiresAuthorization("F_BKPF_BUK", "03")]
public sealed record GetPaymentRunQuery : IQuery<PaymentProposalResult>
{
    public required string RunId { get; init; }
}

public sealed record PaymentProposalResult
{
    public required string RunId { get; init; }
    public required string CompanyCode { get; init; }
    public required string Status { get; init; }
    public required DateOnly RunDate { get; init; }
    public required DateOnly DueBy { get; init; }
    public required string PaymentMethod { get; init; }
    public required string PayingAccount { get; init; }

    public required IReadOnlyList<ProposedPayment> Payments { get; init; }
    public required IReadOnlyList<ExcludedItem> Excluded { get; init; }

    public required decimal TotalToPay { get; init; }
    public required string Currency { get; init; }
}

public sealed record ProposedPayment(
    string BusinessPartner,
    decimal Amount,
    int ItemCount,
    long? PaymentDocumentNumber,
    IReadOnlyList<ProposedPaymentItem> Items);

public sealed record ProposedPaymentItem(
    short FiscalYear, long DocumentNumber, short LineNumber, DateOnly? DueDate, decimal Amount);

public sealed record ExcludedItem(
    string BusinessPartner,
    short FiscalYear,
    long DocumentNumber,
    short LineNumber,
    decimal Amount,
    string Reason);

public sealed record PaymentRunExecutionResult
{
    public required string RunId { get; init; }
    public required string Status { get; init; }
    public required int PaymentsPosted { get; init; }
    public required int ItemsPaid { get; init; }
    public required decimal TotalPaid { get; init; }
    public required IReadOnlyList<long> PaymentDocumentNumbers { get; init; }
}

internal static class PaymentRunErrors
{
    public const string NotFound = "PAYMENT_RUN_NOT_FOUND";
    public const string NotProposed = "PAYMENT_RUN_NOT_PROPOSED";
    public const string NothingToPay = "PAYMENT_RUN_EMPTY";
    public const string UnknownMethod = "UNKNOWN_PAYMENT_METHOD";
    public const string UnknownAccount = "UNKNOWN_HOUSE_BANK_ACCOUNT";
    public const string NeedsApproval = "PAYMENT_RUN_NEEDS_APPROVAL";
    public const string NotAwaitingApproval = "PAYMENT_RUN_NOT_AWAITING_APPROVAL";
}

internal static class PaymentRunApproval
{
    public const string ObjectType = "PaymentRun";
}

// ----------------------------------------------------------------- proposal

public sealed class CreatePaymentProposalHandler(
    S4herpDbContext db,
    IAuthorizationEnforcer authorization,
    INumberRangeAllocator numbers,
    IUserContext user,
    ITenantContext tenant,
    IClock clock,
    ICorrelationContext correlation)
    : ICommandHandler<CreatePaymentProposalCommand, PaymentProposalResult>
{
    public async Task<PaymentProposalResult> HandleAsync(
        CreatePaymentProposalCommand command, CancellationToken ct)
    {
        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", command.CompanyCode), ("ACTVT", "01")], ct);

        var companyCode = await db.Set<CompanyCode>()
            .SingleOrDefaultAsync(c => c.Code == command.CompanyCode, ct)
            ?? throw new NotFoundException($"Company code {command.CompanyCode} does not exist.");

        var method = await db.Set<PaymentMethod>()
            .SingleOrDefaultAsync(m => m.Code == command.PaymentMethod && m.IsActive, ct)
            ?? throw new BusinessRuleException(
                PaymentRunErrors.UnknownMethod,
                $"Payment method {command.PaymentMethod} does not exist or is not active.");

        var account = await db.Set<HouseBankAccount>()
            .Include(a => a.HouseBank)
            .Include(a => a.GLAccount)
            .SingleOrDefaultAsync(a => a.HouseBank.CompanyCodeId == companyCode.Id
                                       && a.HouseBank.Code == command.HouseBank
                                       && a.Code == command.HouseBankAccount
                                       && a.IsActive, ct)
            ?? throw new BusinessRuleException(
                PaymentRunErrors.UnknownAccount,
                $"House bank account {command.HouseBank}/{command.HouseBankAccount} does not " +
                $"exist in company code {command.CompanyCode}.");

        // Direction decides which subledger is in scope: an outgoing method pays
        // vendors, an incoming one collects from customers.
        var accountType = method.Direction == PaymentDirection.Outgoing
            ? AccountType.Vendor
            : AccountType.Customer;

        var candidates = await LoadCandidatesAsync(
            companyCode.Id, accountType, method.RequiresBankDetails, command, ct);

        // A number range rather than a timestamp. The identifier used to end in
        // HHmmss, which reads as unique and is not: two proposals created in the
        // same second for the same company code, date and method produced the
        // same id and the unique index turned that into a 500. Not gapless — a
        // run id is an internal handle, not a document number.
        var sequence = await numbers.AllocateAsync(
            NumberRangeObject.PaymentRun, "PR", null, 0, ct);

        var run = new PaymentRun
        {
            TenantId = tenant.TenantId,
            RunId = $"{companyCode.Code}-{command.RunDate:yyyyMMdd}-{method.Code}-{sequence:D6}",
            CompanyCodeId = companyCode.Id,
            RunDate = command.RunDate,
            DueBy = command.DueBy,
            PaymentMethodCode = method.Code,
            HouseBankAccountId = account.Id,
            Status = PaymentRunStatus.Proposed,
            CreatedBy = user.UserName,
            CreatedAtUtc = clock.UtcNow,
        };

        foreach (var candidate in candidates)
        {
            run.Items.Add(new PaymentRunItem
            {
                TenantId = tenant.TenantId,
                OpenItemId = candidate.Item.Id,
                BusinessPartnerId = candidate.Item.BusinessPartnerId!.Value,
                BusinessPartnerNumber = candidate.PartnerNumber,
                FiscalYear = candidate.Item.FiscalYear,
                DocumentNumber = candidate.Item.DocumentNumber,
                LineNumber = candidate.Item.LineNumber,
                DueDate = candidate.Item.DueDate,
                Amount = Math.Abs(candidate.Item.OpenAmountDocument),
                CurrencyId = candidate.Item.DocumentCurrencyId,
                IsExcluded = candidate.Reason is not null,
                ExclusionReason = candidate.Reason,
                CreatedBy = user.UserName,
                CreatedAtUtc = clock.UtcNow,
            });
        }

        db.Add(run);

        db.Add(new Audit.Domain.AuditLog
        {
            TenantId = tenant.TenantId,
            OccurredAtUtc = clock.UtcNow,
            UserName = user.UserName,
            CompanyCodeId = companyCode.Id,
            Action = Audit.Domain.AuditAction.Create,
            ObjectType = "PaymentRun",
            ObjectId = run.RunId,
            SourceApi = "POST /api/v1/finance/payment-runs",
            TransactionCode = "F110",
            CorrelationId = correlation.CorrelationId,
            Summary = $"Proposed {run.Items.Count(i => !i.IsExcluded)} item(s) to pay, " +
                      $"{run.Items.Count(i => i.IsExcluded)} excluded.",
        });

        var currency = await db.Set<Currency>()
            .Where(c => c.Id == account.CurrencyId).Select(c => c.Code).SingleAsync(ct);

        return Project(run, companyCode.Code, account, currency);
    }

    private sealed record Candidate(OpenItem Item, string PartnerNumber, string? Reason);

    /// <summary>
    /// Everything that could plausibly be paid, each carrying the reason it will
    /// not be. Excluded candidates are kept rather than filtered away: a supplier
    /// going unpaid needs an answer, and "it was not in the selection" is not one.
    /// </summary>
    private async Task<List<Candidate>> LoadCandidatesAsync(
        long companyCodeId, AccountType accountType, bool requiresBankDetails,
        CreatePaymentProposalCommand command, CancellationToken ct)
    {
        var rows = await (
            from item in db.Set<OpenItem>()
            join partner in db.Set<BusinessPartner.Domain.Partner>()
                on item.BusinessPartnerId equals partner.Id
            join facet in db.Set<BusinessPartner.Domain.PartnerCompanyCode>()
                on new { PartnerId = partner.Id, CompanyCodeId = companyCodeId }
                equals new { facet.PartnerId, facet.CompanyCodeId } into facets
            from facet in facets.DefaultIfEmpty()
            where item.CompanyCodeId == companyCodeId
                  && item.AccountType == accountType
                  && item.ClearingStatus != ClearingStatus.Cleared
                  && (command.BusinessPartner == null
                      || partner.PartnerNumber == command.BusinessPartner)
            select new { Item = item, partner.PartnerNumber, Facet = facet })
            .ToListAsync(ct);

        var account = await db.Set<HouseBankAccount>()
            .Include(a => a.HouseBank)
            .SingleAsync(a => a.HouseBank.CompanyCodeId == companyCodeId
                              && a.HouseBank.Code == command.HouseBank
                              && a.Code == command.HouseBankAccount, ct);

        // Which partners can be paid by transfer at all. Loaded once for the whole
        // proposal rather than per candidate: a run with two hundred items would
        // otherwise issue two hundred queries to answer the same question.
        var partnersWithBank = requiresBankDetails
            ? (await db.Set<BusinessPartner.Domain.PartnerBank>()
                .Where(b => b.ValidFrom <= command.RunDate && b.ValidTo >= command.RunDate)
                .Select(b => b.PartnerId)
                .Distinct()
                .ToListAsync(ct))
                .ToHashSet()
            : [];

        return rows.Select(r => new Candidate(r.Item, r.PartnerNumber,
            ExclusionFor(r.Item, r.Facet, account, command, requiresBankDetails,
                partnersWithBank))).ToList();
    }

    private static string? ExclusionFor(
        OpenItem item,
        BusinessPartner.Domain.PartnerCompanyCode? facet,
        HouseBankAccount account,
        CreatePaymentProposalCommand command,
        bool requiresBankDetails,
        HashSet<long> partnersWithBank)
    {
        if (item.DueDate is not { } due)
        {
            return "No due date, so the item cannot be selected by a due-by run.";
        }

        if (due > command.DueBy)
        {
            return $"Not due until {due:yyyy-MM-dd}, after the run's {command.DueBy:yyyy-MM-dd}.";
        }

        if (facet is null)
        {
            return "The partner has no company-code data here.";
        }

        if (facet.IsPaymentBlocked)
        {
            return "The partner is blocked for payment.";
        }

        if (facet.PaymentMethods is not { Length: > 0 } methods
            || !methods.Contains(command.PaymentMethod, StringComparison.OrdinalIgnoreCase))
        {
            return $"The partner does not permit payment method {command.PaymentMethod}.";
        }

        // Paying a USD invoice out of a THB account would need a conversion this
        // run does not do. Refusing is honest; guessing a rate is not.
        if (item.DocumentCurrencyId != account.CurrencyId)
        {
            return "The item's currency differs from the paying account's.";
        }

        // A transfer needs somewhere to transfer to. Caught here rather than at
        // file generation, because by then the payment is posted and the invoice
        // cleared — the ledger would say paid and the bank would never hear of it.
        if (requiresBankDetails && !partnersWithBank.Contains(item.BusinessPartnerId!.Value))
        {
            return $"The partner has no bank details valid on {command.RunDate:yyyy-MM-dd}, " +
                   $"which payment method {command.PaymentMethod} requires.";
        }

        return null;
    }

    internal static PaymentProposalResult Project(
        PaymentRun run, string companyCode, HouseBankAccount account, string currency)
    {
        var payments = run.Items
            .Where(i => !i.IsExcluded)
            .GroupBy(i => i.BusinessPartnerNumber)
            .OrderBy(g => g.Key)
            .Select(g => new ProposedPayment(
                g.Key,
                g.Sum(i => i.Amount),
                g.Count(),
                g.Select(i => i.PaymentDocumentNumber).FirstOrDefault(n => n is not null),
                g.Select(i => new ProposedPaymentItem(
                    i.FiscalYear, i.DocumentNumber, i.LineNumber, i.DueDate, i.Amount)).ToList()))
            .ToList();

        return new PaymentProposalResult
        {
            RunId = run.RunId,
            CompanyCode = companyCode,
            Status = run.Status.ToString(),
            RunDate = run.RunDate,
            DueBy = run.DueBy,
            PaymentMethod = run.PaymentMethodCode,
            PayingAccount = $"{account.HouseBank.Code}/{account.Code}",
            Payments = payments,
            Excluded = run.Items
                .Where(i => i.IsExcluded)
                .OrderBy(i => i.BusinessPartnerNumber).ThenBy(i => i.DocumentNumber)
                .Select(i => new ExcludedItem(
                    i.BusinessPartnerNumber, i.FiscalYear, i.DocumentNumber, i.LineNumber,
                    i.Amount, i.ExclusionReason!))
                .ToList(),
            TotalToPay = payments.Sum(p => p.Amount),
            Currency = currency,
        };
    }
}

// ---------------------------------------------------------------- execution

public sealed class ExecutePaymentRunHandler(
    S4herpDbContext db,
    IDispatcher dispatcher,
    IApprovalService approvals,
    IAuthorizationEnforcer authorization,
    IUserContext user,
    ITenantContext tenant,
    IClock clock,
    ICorrelationContext correlation)
    : ICommandHandler<ExecutePaymentRunCommand, PaymentRunExecutionResult>
{
    public async Task<PaymentRunExecutionResult> HandleAsync(
        ExecutePaymentRunCommand command, CancellationToken ct)
    {
        var run = await LoadAsync(db, command.RunId, ct);

        var companyCode = await db.Set<CompanyCode>().SingleAsync(c => c.Id == run.CompanyCodeId, ct);
        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", companyCode.Code), ("ACTVT", "01")], ct);

        if (run.Status is not (PaymentRunStatus.Proposed or PaymentRunStatus.Approved))
        {
            throw new BusinessRuleException(
                PaymentRunErrors.NotProposed,
                $"Payment run {run.RunId} is {run.Status} and cannot be executed.");
        }

        var payable = run.Items.Where(i => !i.IsExcluded).ToList();
        if (payable.Count == 0)
        {
            throw new BusinessRuleException(
                PaymentRunErrors.NothingToPay,
                $"Payment run {run.RunId} has nothing to pay. Every candidate was excluded.");
        }

        var account = await db.Set<HouseBankAccount>()
            .Include(a => a.HouseBank)
            .Include(a => a.GLAccount)
            .SingleAsync(a => a.Id == run.HouseBankAccountId, ct);

        // An approved run has already been through this. An unapproved one is
        // asked here, once, rather than being trusted: the whole value of the
        // control is that the run cannot route around it by never submitting.
        if (run.Status == PaymentRunStatus.Proposed
            && await approvals.IsApprovalRequiredAsync(
                PaymentRunApproval.ObjectType, run.CompanyCodeId, null,
                payable.Sum(i => i.Amount), account.CurrencyId, ct))
        {
            throw new BusinessRuleException(
                PaymentRunErrors.NeedsApproval,
                $"Payment run {run.RunId} needs approval before it can be executed. " +
                "Submit it first.");
        }

        var documents = new List<long>();
        var total = 0m;

        foreach (var group in payable.GroupBy(i => i.BusinessPartnerNumber).OrderBy(g => g.Key))
        {
            // The run pays by issuing exactly the command a person would, through
            // the dispatcher. Not a copy of the payment logic — the same code,
            // with the same authorisation, validation, clearing and audit. A
            // payment run that posted differently from a manual payment would be
            // a second definition of what a payment is.
            var result = await dispatcher.SendAsync(new PostPaymentCommand
            {
                CompanyCode = companyCode.Code,
                PostingDate = run.RunDate,
                BusinessPartner = group.Key,
                BankAccount = account.GLAccount.AccountNumber,
                Reference = run.RunId,
                HeaderText = $"Payment run {run.RunId}",
                Items = group.Select(i => new PaymentItemSelection
                {
                    FiscalYear = i.FiscalYear,
                    DocumentNumber = i.DocumentNumber,
                    LineNumber = i.LineNumber,
                    Amount = i.Amount,
                }).ToList(),
            }, ct);

            foreach (var item in group)
            {
                item.PaymentDocumentNumber = result.DocumentNumber;
            }

            documents.Add(result.DocumentNumber);
            total += result.Amount;
        }

        run.Status = PaymentRunStatus.Executed;
        run.ExecutedAtUtc = clock.UtcNow;
        run.ExecutedBy = user.UserName;

        db.Add(new Audit.Domain.AuditLog
        {
            TenantId = tenant.TenantId,
            OccurredAtUtc = clock.UtcNow,
            UserName = user.UserName,
            CompanyCodeId = run.CompanyCodeId,
            Action = Audit.Domain.AuditAction.Post,
            ObjectType = "PaymentRun",
            ObjectId = run.RunId,
            SourceApi = "POST /api/v1/finance/payment-runs/{runId}/execute",
            TransactionCode = "F110",
            CorrelationId = correlation.CorrelationId,
            Summary = $"Executed: {documents.Count} payment(s) totalling {total:N2} " +
                      $"settling {payable.Count} item(s).",
        });

        return new PaymentRunExecutionResult
        {
            RunId = run.RunId,
            Status = run.Status.ToString(),
            PaymentsPosted = documents.Count,
            ItemsPaid = payable.Count,
            TotalPaid = total,
            PaymentDocumentNumbers = documents,
        };
    }

    internal static async Task<PaymentRun> LoadAsync(
        S4herpDbContext db, string runId, CancellationToken ct) =>
        await db.Set<PaymentRun>()
            .Include(r => r.Items)
            .SingleOrDefaultAsync(r => r.RunId == runId, ct)
        ?? throw new NotFoundException($"Payment run {runId} does not exist.");
}

public sealed class DeletePaymentProposalHandler(
    S4herpDbContext db,
    IAuthorizationEnforcer authorization,
    IUserContext user,
    ITenantContext tenant,
    IClock clock,
    ICorrelationContext correlation)
    : ICommandHandler<DeletePaymentProposalCommand, PaymentRunExecutionResult>
{
    public async Task<PaymentRunExecutionResult> HandleAsync(
        DeletePaymentProposalCommand command, CancellationToken ct)
    {
        var run = await ExecutePaymentRunHandler.LoadAsync(db, command.RunId, ct);

        var companyCode = await db.Set<CompanyCode>().SingleAsync(c => c.Id == run.CompanyCodeId, ct);
        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", companyCode.Code), ("ACTVT", "06")], ct);

        if (run.Status != PaymentRunStatus.Proposed)
        {
            throw new BusinessRuleException(
                PaymentRunErrors.NotProposed,
                $"Payment run {run.RunId} is {run.Status}. Only a proposal can be discarded; " +
                "an executed run is undone by resetting the clearings and reversing the payments.");
        }

        // Marked, not removed. The proposal is evidence of what was considered on
        // a date, which is exactly what somebody asks for when an invoice was
        // missed.
        run.Status = PaymentRunStatus.Deleted;

        db.Add(new Audit.Domain.AuditLog
        {
            TenantId = tenant.TenantId,
            OccurredAtUtc = clock.UtcNow,
            UserName = user.UserName,
            CompanyCodeId = run.CompanyCodeId,
            Action = Audit.Domain.AuditAction.Delete,
            ObjectType = "PaymentRun",
            ObjectId = run.RunId,
            SourceApi = "DELETE /api/v1/finance/payment-runs/{runId}",
            TransactionCode = "F110",
            CorrelationId = correlation.CorrelationId,
            Summary = "Proposal discarded before execution.",
        });

        return new PaymentRunExecutionResult
        {
            RunId = run.RunId,
            Status = run.Status.ToString(),
            PaymentsPosted = 0,
            ItemsPaid = 0,
            TotalPaid = 0m,
            PaymentDocumentNumbers = [],
        };
    }
}

/// <summary>
/// Submit, approve and reject for a payment run. The same approval engine the
/// journal uses, told a different object type — which is the test of whether
/// increment 3 built a workflow service or a journal-entry service wearing one.
/// </summary>
public sealed class PaymentRunApprovalHandlers(
    S4herpDbContext db,
    IApprovalService approvals,
    IAuthorizationEnforcer authorization,
    IUserContext user,
    ITenantContext tenant,
    IClock clock,
    ICorrelationContext correlation)
    : ICommandHandler<SubmitPaymentRunCommand, PaymentRunApprovalResult>,
        ICommandHandler<ApprovePaymentRunCommand, PaymentRunApprovalResult>,
        ICommandHandler<RejectPaymentRunCommand, PaymentRunApprovalResult>
{
    public async Task<PaymentRunApprovalResult> HandleAsync(
        SubmitPaymentRunCommand command, CancellationToken ct)
    {
        var (run, companyCode, account, total) = await LoadAsync(command.RunId, ct);
        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", companyCode.Code), ("ACTVT", "01")], ct);

        if (run.Status != PaymentRunStatus.Proposed)
        {
            throw new BusinessRuleException(
                PaymentRunErrors.NotProposed,
                $"Payment run {run.RunId} is {run.Status}; only a proposal can be submitted.");
        }

        if (total == 0m)
        {
            throw new BusinessRuleException(
                PaymentRunErrors.NothingToPay,
                $"Payment run {run.RunId} has nothing to pay, so there is nothing to approve.");
        }

        var started = await approvals.StartAsync(new StartApprovalRequest
        {
            ObjectType = PaymentRunApproval.ObjectType,
            ObjectId = run.RunId,
            CompanyCodeId = run.CompanyCodeId,
            DocumentTypeCode = null,
            Amount = total,
            CurrencyId = account.CurrencyId,
            ObjectCreatedBy = run.CreatedBy,
        }, ct);

        // Nothing configured to approve a run this size leaves it Proposed, and
        // the execution gate will reach the same conclusion by the same matcher.
        run.Status = started.Outcome == ApprovalOutcome.NotRequired
            ? PaymentRunStatus.Proposed
            : PaymentRunStatus.PendingApproval;

        WriteAudit(run, Audit.Domain.AuditAction.Submit, "submit",
            started.Outcome == ApprovalOutcome.NotRequired
                ? $"Submitted {total:N2}; no approval rule matched."
                : $"Submitted {total:N2} for approval in {started.Steps.Count} step(s).");

        return Result(run, started.Outcome, total, started.Steps);
    }

    public Task<PaymentRunApprovalResult> HandleAsync(
        ApprovePaymentRunCommand command, CancellationToken ct) =>
        DecideAsync(command.RunId, ApprovalDecision.Approved, command.Comment, ct);

    public Task<PaymentRunApprovalResult> HandleAsync(
        RejectPaymentRunCommand command, CancellationToken ct) =>
        DecideAsync(command.RunId, ApprovalDecision.Rejected, command.Comment, ct);

    private async Task<PaymentRunApprovalResult> DecideAsync(
        string runId, ApprovalDecision decision, string? comment, CancellationToken ct)
    {
        var (run, companyCode, account, total) = await LoadAsync(runId, ct);

        // Seeing the run is a precondition of deciding on it.
        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", companyCode.Code), ("ACTVT", "03")], ct);

        if (run.Status != PaymentRunStatus.PendingApproval)
        {
            throw new BusinessRuleException(
                PaymentRunErrors.NotAwaitingApproval,
                $"Payment run {run.RunId} is {run.Status} and has no approval awaiting a decision.");
        }

        await authorization.RequireAsync(
            "W_APPROVE",
            [("WFTYPE", PaymentRunApproval.ObjectType), ("AMOUNT_TO", AmountLimit.Encode(total))],
            ct);

        var result = await approvals.DecideAsync(new ApprovalDecisionRequest
        {
            ObjectType = PaymentRunApproval.ObjectType,
            ObjectId = run.RunId,
            Decision = decision,
            Comment = comment,
        }, ct);

        run.Status = result.Outcome switch
        {
            // Approved releases it for execution; it does not execute it. Somebody
            // still has to press the button, and that separation is deliberate —
            // an approval that paid immediately would make "approve" and "pay" the
            // same action performed by the approver.
            ApprovalOutcome.Approved => PaymentRunStatus.Approved,
            ApprovalOutcome.Rejected => PaymentRunStatus.Rejected,
            _ => PaymentRunStatus.PendingApproval,
        };

        WriteAudit(run,
            decision == ApprovalDecision.Approved
                ? Audit.Domain.AuditAction.Approve
                : Audit.Domain.AuditAction.Reject,
            decision == ApprovalDecision.Approved ? "approve" : "reject",
            $"Step {result.DecidedStep} {decision.ToString().ToLowerInvariant()}; " +
            $"run {result.Outcome}." + (comment is { Length: > 0 } ? $" Comment: {comment}" : ""));

        return Result(run, result.Outcome, total, result.Steps);
    }

    private async Task<(PaymentRun Run, CompanyCode Company, HouseBankAccount Account, decimal Total)>
        LoadAsync(string runId, CancellationToken ct)
    {
        var run = await ExecutePaymentRunHandler.LoadAsync(db, runId, ct);
        var companyCode = await db.Set<CompanyCode>()
            .SingleAsync(c => c.Id == run.CompanyCodeId, ct);
        var account = await db.Set<HouseBankAccount>()
            .Include(a => a.HouseBank)
            .SingleAsync(a => a.Id == run.HouseBankAccountId, ct);

        return (run, companyCode, account, run.Items.Where(i => !i.IsExcluded).Sum(i => i.Amount));
    }

    private void WriteAudit(PaymentRun run, Audit.Domain.AuditAction action, string verb, string summary) =>
        db.Add(new Audit.Domain.AuditLog
        {
            TenantId = tenant.TenantId,
            OccurredAtUtc = clock.UtcNow,
            UserName = user.UserName,
            CompanyCodeId = run.CompanyCodeId,
            Action = action,
            ObjectType = PaymentRunApproval.ObjectType,
            ObjectId = run.RunId,
            SourceApi = $"POST /api/v1/finance/payment-runs/{{runId}}/{verb}",
            TransactionCode = "F110",
            CorrelationId = correlation.CorrelationId,
            Summary = summary,
        });

    private static PaymentRunApprovalResult Result(
        PaymentRun run, ApprovalOutcome outcome, decimal total,
        IReadOnlyList<ApprovalStepView> steps) => new()
        {
            RunId = run.RunId,
            Status = run.Status.ToString(),
            ApprovalOutcome = outcome.ToString(),
            TotalToPay = total,
            Steps = steps,
        };
}

public sealed class GetPaymentRunQueryHandler(
    S4herpDbContext db, IAuthorizationEnforcer authorization)
    : IQueryHandler<GetPaymentRunQuery, PaymentProposalResult>
{
    public async Task<PaymentProposalResult> HandleAsync(
        GetPaymentRunQuery query, CancellationToken ct)
    {
        var run = await ExecutePaymentRunHandler.LoadAsync(db, query.RunId, ct);

        var companyCode = await db.Set<CompanyCode>().SingleAsync(c => c.Id == run.CompanyCodeId, ct);
        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", companyCode.Code), ("ACTVT", "03")], ct);

        var account = await db.Set<HouseBankAccount>()
            .Include(a => a.HouseBank)
            .SingleAsync(a => a.Id == run.HouseBankAccountId, ct);
        var currency = await db.Set<Currency>()
            .Where(c => c.Id == account.CurrencyId).Select(c => c.Code).SingleAsync(ct);

        return CreatePaymentProposalHandler.Project(run, companyCode.Code, account, currency);
    }
}
