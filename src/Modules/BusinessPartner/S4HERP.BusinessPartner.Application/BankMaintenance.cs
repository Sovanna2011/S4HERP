using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using S4HERP.BuildingBlocks.Application;
using S4HERP.BuildingBlocks.Domain;
using S4HERP.BuildingBlocks.Infrastructure;
using S4HERP.BusinessPartner.Domain;
using S4HERP.Organization.Application;
using S4HERP.Organization.Domain;
using S4HERP.Workflow.Contracts;

namespace S4HERP.BusinessPartner.Application;

/// <summary>
/// Constants shared between the handlers and the seed, so a rename cannot
/// silently disconnect the approval rule from the object it governs.
/// </summary>
public static class BankMaintenance
{
    /// <summary>The workflow object type. Must match the seeded approval rule.</summary>
    public const string ObjectType = "PartnerBank";

    /// <summary>SAP's transaction for vendor bank data; recorded on the audit trail.</summary>
    public const string TransactionCode = "FK02";
}

public static class BankMaintenanceErrors
{
    public const string PartnerNotFound = "PARTNER_NOT_FOUND";
    public const string BankNotFound = "PARTNER_BANK_NOT_FOUND";
    public const string RequestNotFound = "BANK_CHANGE_REQUEST_NOT_FOUND";
    public const string NotPending = "BANK_CHANGE_NOT_PENDING";
    public const string AlreadyPending = "BANK_CHANGE_ALREADY_PENDING";
    public const string DuplicateAccount = "PARTNER_BANK_DUPLICATE";
    public const string ApprovalNotConfigured = "BANK_CHANGE_APPROVAL_NOT_CONFIGURED";
    public const string NothingChanged = "BANK_CHANGE_IS_A_NO_OP";
}

// ----------------------------------------------------------------- commands

/// <summary>
/// Raises a change to a partner's bank details. Applies nothing: the values are
/// staged on the request and the live record is untouched until an approver
/// releases it.
/// </summary>
[RequiresAuthorization("F_BP_BANK", "02")]
public sealed record RequestBankChangeCommand : ICommand<BankChangeRequestResult>
{
    /// <summary>
    /// Deliberately not <c>required</c>: it comes from the route, not the body,
    /// and marking it required makes System.Text.Json reject every request that
    /// correctly omits it. The endpoint always supplies it.
    /// </summary>
    public string PartnerNumber { get; init; } = string.Empty;

    public required string Operation { get; init; }
    public required string Reason { get; init; }

    /// <summary>Which existing record to change or deactivate. Omitted for a create.</summary>
    public string? AccountNumber { get; init; }

    public string? NewCountryCode { get; init; }
    public string? NewBankKey { get; init; }
    public string? NewBankName { get; init; }
    public string? NewAccountNumber { get; init; }
    public string? NewAccountHolder { get; init; }
    public string? NewIban { get; init; }
    public string? NewSwift { get; init; }

    /// <summary>
    /// Nullable so that omitting it means "leave it alone", like every other
    /// field here. As a plain bool it defaulted to false, so a request that only
    /// touched the SWIFT code would also have quietly cleared the partner's
    /// default account — a change nobody typed and the approver could not see.
    /// </summary>
    public bool? NewIsDefault { get; init; }

    /// <summary>
    /// When the account becomes usable. Defaults to today, and may be earlier:
    /// bank details are routinely entered after the invoice that carried them
    /// arrived, and a record dated from today cannot pay an invoice dated last
    /// month — the payment run checks validity against the run date, so a
    /// hardcoded today silently excluded exactly the partner this was meant to
    /// fix. Back-dating is visible to the approver, which is the control on it.
    /// </summary>
    public DateOnly? NewValidFrom { get; init; }
}

/// <summary>
/// Approve or reject. Requires <c>W_APPROVE</c> for this object type, which the
/// role holding <c>F_BP_BANK</c> deliberately does not have.
/// </summary>
public sealed record DecideBankChangeCommand : ICommand<BankChangeRequestResult>
{
    /// <summary>From the route; see <see cref="RequestBankChangeCommand.PartnerNumber"/>.</summary>
    public string RequestId { get; init; } = string.Empty;

    /// <summary>Set by the route, not the caller: approve and reject are separate routes.</summary>
    public bool Approve { get; init; }

    public string? Comment { get; init; }
}

/// <summary>The only thing a decide or withdraw request carries in its body.</summary>
public sealed record DecisionBody
{
    public string? Comment { get; init; }
}

[RequiresAuthorization("F_BP_BANK", "02")]
public sealed record WithdrawBankChangeCommand : ICommand<BankChangeRequestResult>
{
    public string RequestId { get; init; } = string.Empty;
    public string? Comment { get; init; }
}

/// <summary>
/// Reading one change request. Deliberately carries no <c>RequiresAuthorization</c>
/// attribute, because two different authorities can legitimately read it and the
/// attribute expresses only one: the clerk who maintains bank details, and the
/// approver who has to decide this change. Gating it on <c>F_BP_BANK</c> alone
/// meant an approver could see the item in their inbox and then get a 403 on
/// opening it — which is how the approvals screen found this.
/// </summary>
public sealed record GetBankChangeQuery : IQuery<BankChangeRequestResult>
{
    public required string RequestId { get; init; }
}


/// <summary>
/// The partner's live bank details — approved ones, because those are the only
/// ones the table holds.
/// </summary>
[RequiresAuthorization("F_BP_BANK", "03")]
public sealed record GetPartnerBanksQuery : IQuery<PartnerBanksResult>
{
    public required string PartnerNumber { get; init; }
}

// ------------------------------------------------------------------ results

public sealed record BankChangeRequestResult
{
    public required string RequestId { get; init; }
    public required string PartnerNumber { get; init; }
    public required string Operation { get; init; }
    public required string Status { get; init; }
    public required string Reason { get; init; }
    public required string RequestedBy { get; init; }
    public required DateTime RequestedAtUtc { get; init; }
    public string? DecidedBy { get; init; }
    public DateTime? DecidedAtUtc { get; init; }
    public string? DecisionComment { get; init; }

    /// <summary>
    /// What the request proposes, and what the live record held when it was
    /// raised. An approver deciding on "account 8888" without seeing that it
    /// replaces "account 1234" is not being asked a meaningful question.
    /// </summary>
    public required BankDetailView Proposed { get; init; }
    public BankDetailView? Previous { get; init; }

    /// <summary>
    /// When a created account starts being usable. On the result because
    /// back-dating it is a decision the approver should be making knowingly.
    /// </summary>
    public DateOnly? ProposedValidFrom { get; init; }

    public required IReadOnlyList<ApprovalStepView> ApprovalSteps { get; init; }
}

public sealed record BankDetailView(
    string? CountryCode,
    string? BankKey,
    string? BankName,
    string? AccountNumber,
    string? AccountHolder,
    string? Iban,
    string? Swift,
    bool IsDefault);

public sealed record PartnerBanksResult
{
    public required string PartnerNumber { get; init; }
    public required IReadOnlyList<PartnerBankView> Banks { get; init; }
    public required int PendingChangeRequests { get; init; }
}

public sealed record PartnerBankView(
    string CountryCode,
    string BankKey,
    string BankName,
    string AccountNumber,
    string AccountHolder,
    string? Iban,
    string? Swift,
    bool IsDefault,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    bool IsCurrent);

// ----------------------------------------------------------------- handlers

public sealed class RequestBankChangeHandler(
    S4herpDbContext db,
    IAuthorizationEnforcer authorization,
    IApprovalService approvals,
    INumberRangeAllocator numbers,
    IUserContext user,
    ITenantContext tenant,
    IClock clock,
    ICorrelationContext correlation)
    : ICommandHandler<RequestBankChangeCommand, BankChangeRequestResult>
{
    public async Task<BankChangeRequestResult> HandleAsync(
        RequestBankChangeCommand command, CancellationToken ct)
    {
        await authorization.RequireAsync("F_BP_BANK", [("ACTVT", "02")], ct);

        var operation = ParseOperation(command.Operation);

        var partner = await db.Set<Partner>()
            .SingleOrDefaultAsync(p => p.PartnerNumber == command.PartnerNumber, ct)
            ?? throw new BusinessRuleException(
                BankMaintenanceErrors.PartnerNotFound,
                $"Business partner {command.PartnerNumber} does not exist.");

        var today = DateOnly.FromDateTime(clock.UtcNow);

        // One pending request per partner. Two concurrent changes to the same
        // partner's accounts would be applied in whichever order they happened to
        // be approved, and the second would silently overwrite the first — with
        // both approvers believing they had released the change they read.
        var alreadyPending = await db.Set<PartnerBankChangeRequest>()
            .AnyAsync(r => r.PartnerId == partner.Id
                           && r.Status == BankChangeStatus.Pending, ct);

        if (alreadyPending)
        {
            throw new BusinessRuleException(
                BankMaintenanceErrors.AlreadyPending,
                $"Business partner {command.PartnerNumber} already has a bank change " +
                "awaiting approval. Decide or withdraw it first.");
        }

        PartnerBank? target = null;
        if (operation is BankChangeOperation.Change or BankChangeOperation.Deactivate)
        {
            if (string.IsNullOrWhiteSpace(command.AccountNumber))
            {
                throw new RequestValidationException(
                    [new RuleViolation(
                        "accountNumber", "REQUIRED",
                        $"A {command.Operation.ToLowerInvariant()} must name the account it applies to.")]);
            }

            target = await db.Set<PartnerBank>()
                .SingleOrDefaultAsync(x => x.PartnerId == partner.Id
                                           && x.AccountNumber == command.AccountNumber
                                           && x.ValidTo > today, ct)
                ?? throw new BusinessRuleException(
                    BankMaintenanceErrors.BankNotFound,
                    $"Business partner {command.PartnerNumber} has no current bank account " +
                    $"{command.AccountNumber}.");
        }

        var proposed = BuildProposal(command, operation, target);

        if (operation is BankChangeOperation.Create or BankChangeOperation.Change)
        {
            ValidateProposal(proposed, operation);
            await RequireNoDuplicateAsync(partner.Id, proposed, target, today, ct);
        }

        if (operation == BankChangeOperation.Change && NothingChanges(proposed, target!))
        {
            // A request that changes nothing still costs an approver their
            // attention, and a stream of them is how a control stops being read.
            throw new BusinessRuleException(
                BankMaintenanceErrors.NothingChanged,
                "The request proposes the values the record already holds.");
        }

        var sequence = await numbers.AllocateAsync(
            NumberRangeObject.PartnerBankChange, "BK", null, 0, ct);
        var requestId = $"BNK-{sequence:D8}";
        var now = clock.UtcNow;

        var request = new PartnerBankChangeRequest
        {
            TenantId = tenant.TenantId,
            RequestId = requestId,
            PartnerId = partner.Id,
            Operation = operation,
            Status = BankChangeStatus.Pending,
            PartnerBankId = target?.Id,
            CountryCode = proposed.CountryCode,
            BankKey = proposed.BankKey,
            BankName = proposed.BankName,
            AccountNumber = proposed.AccountNumber,
            AccountHolder = proposed.AccountHolder,
            Iban = proposed.Iban,
            Swift = proposed.Swift,
            IsDefault = proposed.IsDefault,
            ValidFrom = operation == BankChangeOperation.Create
                ? command.NewValidFrom ?? today
                : null,
            PreviousValues = target is null ? null : JsonSerializer.Serialize(Snapshot(target)),
            Reason = command.Reason,
            RequestedBy = user.UserName,
            RequestedAtUtc = now,
            CreatedBy = user.UserName,
        };

        db.Add(request);

        // Client-level, and with no amount: a bank change is approved because of
        // what it is, not what it is worth. The engine used to require both a
        // company code and a currency-denominated amount, which is why it needed
        // generalising before this object could use it.
        var started = await approvals.StartAsync(new StartApprovalRequest
        {
            ObjectType = BankMaintenance.ObjectType,
            ObjectId = requestId,
            CompanyCodeId = null,
            DocumentTypeCode = null,
            Amount = null,
            CurrencyId = null,
            ObjectCreatedBy = user.UserName,
        }, ct);

        if (started.Outcome == ApprovalOutcome.NotRequired)
        {
            // Refusing is the only safe answer. Applying the change on the
            // requester's own authority is exactly the unreviewed edit this
            // whole path exists to prevent, and silently doing it because the
            // rule table happens to be empty would be the worst kind of failure:
            // the control reports as present and does nothing.
            throw new BusinessRuleException(
                BankMaintenanceErrors.ApprovalNotConfigured,
                "No approval rule governs bank detail changes, so this change cannot be " +
                "released by anyone. Configure a rule for object type " +
                $"{BankMaintenance.ObjectType} before maintaining bank details.");
        }

        db.Add(new Audit.Domain.AuditLog
        {
            TenantId = tenant.TenantId,
            OccurredAtUtc = now,
            UserName = user.UserName,
            Action = Audit.Domain.AuditAction.Create,
            ObjectType = "PartnerBankChangeRequest",
            ObjectId = requestId,
            SourceApi = "POST /api/v1/business-partners/{partnerNumber}/bank-details/changes",
            TransactionCode = BankMaintenance.TransactionCode,
            CorrelationId = correlation.CorrelationId,
            Summary = $"{operation} on {command.PartnerNumber} raised for approval: {command.Reason}",
        });

        return Project(request, partner.PartnerNumber, started.Steps);
    }

    private static BankDetailView BuildProposal(
        RequestBankChangeCommand command, BankChangeOperation operation, PartnerBank? target)
    {
        if (operation == BankChangeOperation.Deactivate)
        {
            return Snapshot(target!);
        }

        // A change states only the fields it changes. Everything unstated keeps
        // the value it has, so a partial request cannot blank a field by
        // omission — which for SWIFT or IBAN would produce a file the bank
        // rejects, days later, with nobody having decided to remove it.
        return new BankDetailView(
            command.NewCountryCode ?? target?.CountryCode,
            command.NewBankKey ?? target?.BankKey,
            command.NewBankName ?? target?.BankName,
            command.NewAccountNumber ?? target?.AccountNumber,
            command.NewAccountHolder ?? target?.AccountHolder,
            command.NewIban ?? target?.Iban,
            command.NewSwift ?? target?.Swift,
            command.NewIsDefault ?? target?.IsDefault ?? false);
    }

    private static void ValidateProposal(BankDetailView p, BankChangeOperation operation)
    {
        var violations = new List<RuleViolation>();

        void Require(string? value, string field, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                violations.Add(new RuleViolation(field, "REQUIRED", $"{field} is required."));
            }
            else if (value.Length > max)
            {
                violations.Add(new RuleViolation(
                    field, "TOO_LONG", $"{field} is limited to {max} characters."));
            }
        }

        Require(p.CountryCode, "newCountryCode", 2);
        Require(p.BankKey, "newBankKey", 15);
        Require(p.BankName, "newBankName", 120);
        Require(p.AccountNumber, "newAccountNumber", 35);
        Require(p.AccountHolder, "newAccountHolder", 60);

        if (p.Iban is { Length: > 34 })
        {
            violations.Add(new RuleViolation("newIban", "TOO_LONG", "An IBAN is at most 34 characters."));
        }

        if (p.Swift is { Length: > 0 } swift && swift.Length is not (8 or 11))
        {
            // ISO 9362: eight characters, or eleven with a branch code. Anything
            // else is a typo, and a typo here routes a payment nowhere.
            violations.Add(new RuleViolation(
                "newSwift", "INVALID_SWIFT", "A SWIFT/BIC code is 8 or 11 characters."));
        }

        if (operation == BankChangeOperation.Create && p.CountryCode is { Length: > 0 } country
            && !country.All(char.IsAsciiLetterUpper))
        {
            violations.Add(new RuleViolation(
                "newCountryCode", "INVALID_COUNTRY", "A country code is two upper-case letters."));
        }

        if (violations.Count > 0)
        {
            throw new RequestValidationException(violations);
        }
    }

    private async Task RequireNoDuplicateAsync(
        long partnerId, BankDetailView proposed, PartnerBank? target, DateOnly today,
        CancellationToken ct)
    {
        // UX_BpBank_Account already forbids this at the database, which is what
        // makes it true under concurrency. Checking here as well turns a 500 into
        // a sentence naming the partner that already holds the account — and that
        // sentence is the interesting one, because the same account number under
        // two partners is a duplicate-vendor smell, not a typo.
        var clash = await db.Set<PartnerBank>()
            .Where(x => x.CountryCode == proposed.CountryCode
                        && x.BankKey == proposed.BankKey
                        && x.AccountNumber == proposed.AccountNumber
                        && x.ValidTo > today
                        && (target == null || x.Id != target.Id))
            .Select(x => new { x.PartnerId, x.Partner.PartnerNumber })
            .FirstOrDefaultAsync(ct);

        if (clash is not null)
        {
            throw new BusinessRuleException(
                BankMaintenanceErrors.DuplicateAccount,
                clash.PartnerId == partnerId
                    ? $"Business partner {clash.PartnerNumber} already holds account " +
                      $"{proposed.AccountNumber} at that bank."
                    : $"Account {proposed.AccountNumber} at that bank already belongs to " +
                      $"business partner {clash.PartnerNumber}.");
        }
    }

    private static bool NothingChanges(BankDetailView p, PartnerBank t) =>
        p == Snapshot(t);

    internal static BankDetailView Snapshot(PartnerBank b) => new(
        b.CountryCode, b.BankKey, b.BankName, b.AccountNumber,
        b.AccountHolder, b.Iban, b.Swift, b.IsDefault);

    internal static BankChangeOperation ParseOperation(string value) =>
        Enum.TryParse<BankChangeOperation>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new RequestValidationException(
                [new RuleViolation(
                    "operation", "INVALID_OPERATION",
                    "Operation must be Create, Change or Deactivate.")]);

    internal static BankChangeRequestResult Project(
        PartnerBankChangeRequest r, string partnerNumber, IReadOnlyList<ApprovalStepView> steps) =>
        new()
        {
            RequestId = r.RequestId,
            PartnerNumber = partnerNumber,
            Operation = r.Operation.ToString(),
            Status = r.Status.ToString(),
            Reason = r.Reason,
            RequestedBy = r.RequestedBy,
            RequestedAtUtc = r.RequestedAtUtc,
            DecidedBy = r.DecidedBy,
            DecidedAtUtc = r.DecidedAtUtc,
            DecisionComment = r.DecisionComment,
            Proposed = new BankDetailView(
                r.CountryCode, r.BankKey, r.BankName, r.AccountNumber,
                r.AccountHolder, r.Iban, r.Swift, r.IsDefault),
            Previous = r.PreviousValues is null
                ? null
                : JsonSerializer.Deserialize<BankDetailView>(r.PreviousValues),
            ProposedValidFrom = r.ValidFrom,
            ApprovalSteps = steps,
        };
}

/// <summary>
/// Records the approver's decision and, on approval, applies the staged values
/// to the live record. This is the only place <see cref="PartnerBank"/> is
/// written, which is what makes "every bank detail was approved by someone other
/// than the person who asked for it" an invariant rather than a convention.
/// </summary>
public sealed class DecideBankChangeHandler(
    S4herpDbContext db,
    IAuthorizationEnforcer authorization,
    IApprovalService approvals,
    IUserContext user,
    ITenantContext tenant,
    IClock clock,
    ICorrelationContext correlation)
    : ICommandHandler<DecideBankChangeCommand, BankChangeRequestResult>
{
    public async Task<BankChangeRequestResult> HandleAsync(
        DecideBankChangeCommand command, CancellationToken ct)
    {
        // Not F_BP_BANK. Approving a bank change is a different authority from
        // maintaining one, and a role holding both would make the second
        // signature available to the person who wanted the first.
        await authorization.RequireAsync(
            "W_APPROVE", [("WFTYPE", BankMaintenance.ObjectType)], ct);

        var request = await db.Set<PartnerBankChangeRequest>()
            .Include(r => r.Partner)
            .SingleOrDefaultAsync(r => r.RequestId == command.RequestId, ct)
            ?? throw new BusinessRuleException(
                BankMaintenanceErrors.RequestNotFound,
                $"Bank change request {command.RequestId} does not exist.");

        if (request.Status != BankChangeStatus.Pending)
        {
            throw new BusinessRuleException(
                BankMaintenanceErrors.NotPending,
                $"Bank change request {command.RequestId} is {request.Status} and cannot be decided again.");
        }

        // Maker-checker lives in the approval service, so this cannot drift from
        // how the control behaves for journal entries and payment runs.
        var decision = await approvals.DecideAsync(new ApprovalDecisionRequest
        {
            ObjectType = BankMaintenance.ObjectType,
            ObjectId = request.RequestId,
            Decision = command.Approve ? ApprovalDecision.Approved : ApprovalDecision.Rejected,
            Comment = command.Comment,
        }, ct);

        var now = clock.UtcNow;
        var summary = $"{request.Operation} on {request.Partner.PartnerNumber}";

        if (decision.Outcome == ApprovalOutcome.Rejected)
        {
            request.Status = BankChangeStatus.Rejected;
            request.DecidedBy = user.UserName;
            request.DecidedAtUtc = now;
            request.DecisionComment = command.Comment;
            request.ModifiedBy = user.UserName;
            request.ModifiedAtUtc = now;
            WriteAudit(request, Audit.Domain.AuditAction.Change, $"{summary} rejected: {command.Comment}");
            return RequestBankChangeHandler.Project(
                request, request.Partner.PartnerNumber, decision.Steps);
        }

        if (decision.Outcome == ApprovalOutcome.Pending)
        {
            // Multi-level approval: this step is signed, the change is not
            // released. Nothing is applied and the request stays pending.
            WriteAudit(request, Audit.Domain.AuditAction.Change,
                $"{summary} approved at step {decision.DecidedStep}, " +
                $"{decision.RemainingSteps} remaining");
            return RequestBankChangeHandler.Project(
                request, request.Partner.PartnerNumber, decision.Steps);
        }

        await ApplyAsync(request, now, ct);

        request.Status = BankChangeStatus.Applied;
        request.DecidedBy = user.UserName;
        request.DecidedAtUtc = now;
        request.DecisionComment = command.Comment;
        request.ModifiedBy = user.UserName;
        request.ModifiedAtUtc = now;

        WriteAudit(request, Audit.Domain.AuditAction.Change, $"{summary} approved and applied");

        return RequestBankChangeHandler.Project(
            request, request.Partner.PartnerNumber, decision.Steps);
    }

    private async Task ApplyAsync(PartnerBankChangeRequest r, DateTime now, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(now);

        switch (r.Operation)
        {
            case BankChangeOperation.Create:
                db.Add(new PartnerBank
                {
                    TenantId = tenant.TenantId,
                    PartnerId = r.PartnerId,
                    CountryCode = r.CountryCode!,
                    BankKey = r.BankKey!,
                    BankName = r.BankName!,
                    AccountNumber = r.AccountNumber!,
                    AccountHolder = r.AccountHolder!,
                    Iban = r.Iban,
                    Swift = r.Swift,
                    IsDefault = r.IsDefault,
                    ValidFrom = r.ValidFrom ?? today,
                    CreatedBy = user.UserName,
                });
                break;

            case BankChangeOperation.Change:
            {
                var bank = await Target(r, ct);
                bank.CountryCode = r.CountryCode!;
                bank.BankKey = r.BankKey!;
                bank.BankName = r.BankName!;
                bank.AccountNumber = r.AccountNumber!;
                bank.AccountHolder = r.AccountHolder!;
                bank.Iban = r.Iban;
                bank.Swift = r.Swift;
                bank.IsDefault = r.IsDefault;
                bank.ModifiedBy = user.UserName;
                bank.ModifiedAtUtc = now;
                break;
            }

            case BankChangeOperation.Deactivate:
            {
                // Closed, not deleted. A payment already made to this account is
                // a fact, and the row is what makes it explicable later.
                var bank = await Target(r, ct);
                bank.ValidTo = today;
                bank.IsDefault = false;
                bank.ModifiedBy = user.UserName;
                bank.ModifiedAtUtc = now;
                break;
            }
        }

        // At most one default per partner, enforced here rather than by an index:
        // the rule is "the newly approved one wins", which an index can refuse
        // but cannot resolve.
        if (r.IsDefault && r.Operation != BankChangeOperation.Deactivate)
        {
            await db.Set<PartnerBank>()
                .Where(x => x.PartnerId == r.PartnerId
                            && x.IsDefault
                            && (r.PartnerBankId == null || x.Id != r.PartnerBankId))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDefault, false), ct);
        }
    }

    private async Task<PartnerBank> Target(PartnerBankChangeRequest r, CancellationToken ct) =>
        await db.Set<PartnerBank>().SingleOrDefaultAsync(x => x.Id == r.PartnerBankId, ct)
        ?? throw new BusinessRuleException(
            BankMaintenanceErrors.BankNotFound,
            $"The bank record {r.RequestId} applies to no longer exists.");

    private void WriteAudit(PartnerBankChangeRequest r, Audit.Domain.AuditAction action, string summary) =>
        db.Add(new Audit.Domain.AuditLog
        {
            TenantId = tenant.TenantId,
            OccurredAtUtc = clock.UtcNow,
            UserName = user.UserName,
            Action = action,
            ObjectType = "PartnerBankChangeRequest",
            ObjectId = r.RequestId,
            SourceApi = "POST /api/v1/business-partners/bank-details/changes/{requestId}/decide",
            TransactionCode = BankMaintenance.TransactionCode,
            CorrelationId = correlation.CorrelationId,
            Summary = summary,
        });
}

public sealed class WithdrawBankChangeHandler(
    S4herpDbContext db,
    IAuthorizationEnforcer authorization,
    IApprovalService approvals,
    IUserContext user,
    ITenantContext tenant,
    IClock clock,
    ICorrelationContext correlation)
    : ICommandHandler<WithdrawBankChangeCommand, BankChangeRequestResult>
{
    public async Task<BankChangeRequestResult> HandleAsync(
        WithdrawBankChangeCommand command, CancellationToken ct)
    {
        await authorization.RequireAsync("F_BP_BANK", [("ACTVT", "02")], ct);

        var request = await db.Set<PartnerBankChangeRequest>()
            .Include(r => r.Partner)
            .SingleOrDefaultAsync(r => r.RequestId == command.RequestId, ct)
            ?? throw new BusinessRuleException(
                BankMaintenanceErrors.RequestNotFound,
                $"Bank change request {command.RequestId} does not exist.");

        if (request.Status != BankChangeStatus.Pending)
        {
            throw new BusinessRuleException(
                BankMaintenanceErrors.NotPending,
                $"Bank change request {command.RequestId} is {request.Status} and cannot be withdrawn.");
        }

        // The approval service enforces that only the requester may withdraw,
        // and refuses once a step has been approved.
        var decision = await approvals.WithdrawAsync(
            BankMaintenance.ObjectType, request.RequestId, command.Comment, ct);

        var now = clock.UtcNow;
        request.Status = BankChangeStatus.Withdrawn;
        request.DecidedBy = user.UserName;
        request.DecidedAtUtc = now;
        request.DecisionComment = command.Comment;
        request.ModifiedBy = user.UserName;
        request.ModifiedAtUtc = now;

        db.Add(new Audit.Domain.AuditLog
        {
            TenantId = tenant.TenantId,
            OccurredAtUtc = now,
            UserName = user.UserName,
            Action = Audit.Domain.AuditAction.Change,
            ObjectType = "PartnerBankChangeRequest",
            ObjectId = request.RequestId,
            SourceApi = "POST /api/v1/business-partners/bank-details/changes/{requestId}/withdraw",
            TransactionCode = BankMaintenance.TransactionCode,
            CorrelationId = correlation.CorrelationId,
            Summary = $"{request.Operation} on {request.Partner.PartnerNumber} withdrawn by the requester",
        });

        return RequestBankChangeHandler.Project(
            request, request.Partner.PartnerNumber, decision.Steps);
    }
}

public sealed class GetBankChangeQueryHandler(
    S4herpDbContext db, IAuthorizationEnforcer authorization, IApprovalService approvals)
    : IQueryHandler<GetBankChangeQuery, BankChangeRequestResult>
{
    public async Task<BankChangeRequestResult> HandleAsync(GetBankChangeQuery query, CancellationToken ct)
    {
        // Either authority is enough, and neither implies the other. An approver
        // holds no F_BP_BANK by design — that is SOD004 — so requiring it would
        // make the approval impossible to make informedly, which is worse than
        // useless: it would leave the approver clicking approve on a request they
        // were not allowed to read.
        var mayMaintain = await authorization.IsAuthorizedAsync(
            "F_BP_BANK", [("ACTVT", "03")], ct);

        if (!mayMaintain)
        {
            await authorization.RequireAsync(
                "W_APPROVE", [("WFTYPE", BankMaintenance.ObjectType)], ct);
        }

        var request = await db.Set<PartnerBankChangeRequest>()
            .AsNoTracking()
            .Include(r => r.Partner)
            .SingleOrDefaultAsync(r => r.RequestId == query.RequestId, ct)
            ?? throw new NotFoundException(
                $"Bank change request {query.RequestId} does not exist.");

        var state = await approvals.GetAsync(BankMaintenance.ObjectType, request.RequestId, ct);

        return RequestBankChangeHandler.Project(
            request, request.Partner.PartnerNumber, state?.Steps ?? []);
    }
}

public sealed class GetPartnerBanksQueryHandler(
    S4herpDbContext db, IAuthorizationEnforcer authorization, IClock clock)
    : IQueryHandler<GetPartnerBanksQuery, PartnerBanksResult>
{
    public async Task<PartnerBanksResult> HandleAsync(GetPartnerBanksQuery query, CancellationToken ct)
    {
        await authorization.RequireAsync("F_BP_BANK", [("ACTVT", "03")], ct);

        var partner = await db.Set<Partner>()
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.PartnerNumber == query.PartnerNumber, ct)
            ?? throw new NotFoundException(
                $"Business partner {query.PartnerNumber} does not exist.");

        var today = DateOnly.FromDateTime(clock.UtcNow);

        var banks = await db.Set<PartnerBank>()
            .AsNoTracking()
            .Where(x => x.PartnerId == partner.Id)
            .OrderByDescending(x => x.IsDefault).ThenBy(x => x.AccountNumber)
            .Select(x => new PartnerBankView(
                x.CountryCode, x.BankKey, x.BankName, x.AccountNumber, x.AccountHolder,
                x.Iban, x.Swift, x.IsDefault, x.ValidFrom, x.ValidTo,
                x.ValidFrom <= today && x.ValidTo > today))
            .ToListAsync(ct);

        // Surfaced rather than left implicit: somebody reading these details needs
        // to know a change to them is in flight, not discover it when the numbers
        // move under a payment run.
        var pending = await db.Set<PartnerBankChangeRequest>()
            .CountAsync(r => r.PartnerId == partner.Id
                             && r.Status == BankChangeStatus.Pending, ct);

        return new PartnerBanksResult
        {
            PartnerNumber = partner.PartnerNumber,
            Banks = banks,
            PendingChangeRequests = pending,
        };
    }
}


// -------------------------------------------------------------- inbox describer

/// <summary>
/// What a pending bank change looks like in the approvals inbox. The subtitle
/// carries the actual change — "account 0001-00-123456-1 → 0002-00-999888-7" —
/// because that is the decision, and an inbox row saying only "Change on
/// 1000000002" asks the approver to open every item to find out which ones
/// matter.
/// </summary>
public sealed class PartnerBankChangeDescriber(S4herpDbContext db) : IApprovalObjectDescriber
{
    public string ObjectType => BankMaintenance.ObjectType;

    public async Task<IReadOnlyDictionary<string, ApprovalObjectDescription>> DescribeAsync(
        IReadOnlyList<string> objectIds, CancellationToken cancellationToken = default)
    {
        var requests = await db.Set<PartnerBankChangeRequest>()
            .AsNoTracking()
            .Include(r => r.Partner)
            .Where(r => objectIds.Contains(r.RequestId))
            .Select(r => new
            {
                r.RequestId,
                r.Operation,
                r.Partner.PartnerNumber,
                PartnerName = r.Partner.Name ?? r.Partner.LastName,
                r.AccountNumber,
                r.PreviousValues,
                r.Reason,
            })
            .ToListAsync(cancellationToken);

        return requests.ToDictionary(
            r => r.RequestId,
            r =>
            {
                var previous = r.PreviousValues is null
                    ? null
                    : JsonSerializer.Deserialize<BankDetailView>(r.PreviousValues);

                var what = r.Operation switch
                {
                    BankChangeOperation.Create =>
                        $"New account {r.AccountNumber}",
                    BankChangeOperation.Deactivate =>
                        $"Close account {r.AccountNumber}",
                    _ when previous?.AccountNumber is { } was && was != r.AccountNumber =>
                        $"Account {was} → {r.AccountNumber}",
                    // A change that leaves the account number alone is a lower-risk
                    // edit, and saying so is more useful than repeating the number.
                    _ => $"Details of account {r.AccountNumber}",
                };

                return new ApprovalObjectDescription(
                    $"{r.PartnerName} ({r.PartnerNumber})",
                    $"{what} · {r.Reason}");
            });
    }
}
