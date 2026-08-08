using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using S4HERP.BuildingBlocks.Application;
using S4HERP.BuildingBlocks.Infrastructure;
using S4HERP.Finance.Domain;
using S4HERP.Organization.Domain;

namespace S4HERP.Finance.Application;

// ----------------------------------------------------------------- commands

/// <summary>
/// Imports an ISO 20022 pain.002 payment status report — the bank's answer to a
/// pain.001 this system sent.
///
/// Until this existed an executed payment run was assumed successful. Every step
/// up to the bank was controlled and audited, and what came back was invisible:
/// a refused transfer left the ledger saying paid, discoverable only by somebody
/// reconciling a statement by hand.
/// </summary>
[RequiresAuthorization("F_BKPF_BUK", "01")]
public sealed record ImportPaymentStatusCommand : ICommand<PaymentStatusReportResult>
{
    /// <summary>The pain.002 document, as the bank sent it.</summary>
    public required string Content { get; init; }
}

/// <summary>
/// Undoes a payment the bank refused: reverses the payment document and reopens
/// the invoices it cleared.
///
/// Deliberately a separate, explicit action rather than something the import
/// does on its own. Importing a file is a clerical act; reversing a posted
/// document is an accounting one, and a bank file that arrives at 3am should not
/// be able to post to the ledger unattended. The import makes the discrepancy
/// impossible to miss; a person decides what to do about it.
/// </summary>
[RequiresAuthorization("F_BKPF_BUK", "01")]
public sealed record ResolveRejectedPaymentCommand : ICommand<ResolveRejectedPaymentResult>
{
    /// <summary>
    /// From the route, so deliberately not <c>required</c>: marking a
    /// route-supplied field required makes System.Text.Json reject every request
    /// that correctly omits it from the body. Same mistake as increment 9, made
    /// twice — hence <see cref="ResolveRejectionBody"/>, so the body type cannot
    /// carry route fields at all.
    /// </summary>
    public string EndToEndId { get; init; } = string.Empty;

    /// <summary>Recorded on the reversal, because "why was this reversed" outlives everyone.</summary>
    public string? Comment { get; init; }
}

/// <summary>Everything a resolve request legitimately carries in its body.</summary>
public sealed record ResolveRejectionBody
{
    public string? Comment { get; init; }
}

[RequiresAuthorization("F_BKPF_BUK", "03")]
public sealed record GetPaymentStatusReportQuery : IQuery<PaymentStatusReportResult>
{
    public required string MessageId { get; init; }
}

/// <summary>
/// What the bank refused that the ledger still shows as paid. The one query this
/// increment exists to make answerable.
/// </summary>
[RequiresAuthorization("F_BKPF_BUK", "03")]
public sealed record GetOutstandingRejectionsQuery : IQuery<IReadOnlyList<RejectedPaymentView>>
{
    public string? CompanyCode { get; init; }

    /// <summary>Resolved rejections are history; by default only the open ones are returned.</summary>
    public bool IncludeResolved { get; init; }
}

/// <summary>
/// Everything the bank has said about one run's payments.
///
/// A bank may revise itself — pending on Monday, settled on Tuesday — so this
/// keeps the latest word per transaction rather than every word. The earlier
/// reports are still stored whole; this is the answer to "where does this run
/// stand", not to "what has the bank ever said".
/// </summary>
[RequiresAuthorization("F_BKPF_BUK", "03")]
public sealed record GetRunBankStatusQuery : IQuery<RunBankStatusResult>
{
    public required string RunId { get; init; }
}

public sealed record RunBankStatusResult
{
    public required string RunId { get; init; }
    public required int Accepted { get; init; }
    public required int Rejected { get; init; }
    public required int Pending { get; init; }
    public required int Unmatched { get; init; }
    public required IReadOnlyList<PaymentStatusItemView> Items { get; init; }
}

// ------------------------------------------------------------------ results

public sealed record PaymentStatusReportResult
{
    public required string MessageId { get; init; }
    public required string OriginalMessageId { get; init; }
    public required string RunId { get; init; }
    public required string Format { get; init; }
    public string? GroupStatus { get; init; }
    public required DateTime ImportedAtUtc { get; init; }
    public required string ImportedBy { get; init; }

    public required int Accepted { get; init; }
    public required int Rejected { get; init; }
    public required int Pending { get; init; }

    /// <summary>
    /// Transactions the bank reported that this system could not place. Never
    /// silently dropped: a status for a payment we cannot find means either the
    /// bank is confused or we are, and both need a person.
    /// </summary>
    public required int Unmatched { get; init; }

    public required bool AlreadyImported { get; init; }
    public required IReadOnlyList<PaymentStatusItemView> Items { get; init; }
}

public sealed record PaymentStatusItemView(
    string EndToEndId,
    string Status,
    string? ReasonCode,
    string? ReasonText,
    decimal? Amount,
    string? CompanyCode,
    short? FiscalYear,
    long? PaymentDocumentNumber,
    bool IsResolved,
    long? ReversalDocumentNumber);

public sealed record RejectedPaymentView(
    string EndToEndId,
    string ReportMessageId,
    string RunId,
    string? ReasonCode,
    string? ReasonText,
    decimal? Amount,
    string? CompanyCode,
    short? FiscalYear,
    long? PaymentDocumentNumber,
    DateTime ReportedAtUtc,
    bool IsResolved);

public sealed record ResolveRejectedPaymentResult
{
    public required string EndToEndId { get; init; }
    public required string CompanyCode { get; init; }
    public required long PaymentDocumentNumber { get; init; }
    public required long ReversalDocumentNumber { get; init; }
    public required int ItemsReopened { get; init; }
}

internal static class PaymentStatusErrors
{
    public const string Malformed = "PAYMENT_STATUS_MALFORMED";
    public const string UnknownOriginalMessage = "PAYMENT_STATUS_UNKNOWN_ORIGINAL";
    public const string NotFound = "PAYMENT_STATUS_NOT_FOUND";
    public const string NotRejected = "PAYMENT_NOT_REJECTED";
    public const string AlreadyResolved = "PAYMENT_REJECTION_ALREADY_RESOLVED";
    public const string Unmatched = "PAYMENT_STATUS_UNMATCHED";
}

// ------------------------------------------------------------------- parsing

/// <summary>
/// pain.002 reader. Namespace-agnostic on purpose: banks send
/// pain.002.001.10, .03 and older, and refusing a file because its minor version
/// is not the one we generate would reject perfectly good bank traffic. The
/// element names this reads have been stable across all of them.
/// </summary>
internal static class PaymentStatusReader
{
    internal sealed record ParsedReport(
        string MessageId,
        string OriginalMessageId,
        string Format,
        DateTime? CreatedAtUtc,
        BankTransactionStatus? GroupStatus,
        IReadOnlyList<ParsedItem> Items);

    internal sealed record ParsedItem(
        string EndToEndId,
        BankTransactionStatus Status,
        string? ReasonCode,
        string? ReasonText,
        decimal? Amount);

    public static ParsedReport Parse(string content)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(content);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new BusinessRuleException(
                PaymentStatusErrors.Malformed, $"The file is not well-formed XML: {ex.Message}");
        }

        var root = document.Root
            ?? throw new BusinessRuleException(
                PaymentStatusErrors.Malformed, "The file has no root element.");

        var ns = root.GetDefaultNamespace();
        var report = root.Element(ns + "CstmrPmtStsRpt")
            ?? throw new BusinessRuleException(
                PaymentStatusErrors.Malformed,
                "The file is not a customer payment status report: no CstmrPmtStsRpt element.");

        var header = report.Element(ns + "GrpHdr");
        var messageId = header?.Element(ns + "MsgId")?.Value;
        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new BusinessRuleException(
                PaymentStatusErrors.Malformed, "The report has no GrpHdr/MsgId.");
        }

        var original = report.Element(ns + "OrgnlGrpInfAndSts");
        var originalMessageId = original?.Element(ns + "OrgnlMsgId")?.Value;
        if (string.IsNullOrWhiteSpace(originalMessageId))
        {
            throw new BusinessRuleException(
                PaymentStatusErrors.Malformed,
                "The report does not say which message it answers: no OrgnlGrpInfAndSts/OrgnlMsgId.");
        }

        var items = new List<ParsedItem>();

        // Transaction status blocks sit under OrgnlPmtInfAndSts, but a bank that
        // rejects the whole file may put them at the top. Descendants covers both
        // without caring which shape arrived.
        foreach (var transaction in report.Descendants(ns + "TxInfAndSts"))
        {
            var endToEndId = transaction.Element(ns + "OrgnlEndToEndId")?.Value;
            if (string.IsNullOrWhiteSpace(endToEndId))
            {
                // Nothing to match it to, and nothing useful to record. The count
                // still comes out wrong against the file, which is the honest
                // outcome — see the malformed-transaction check below.
                continue;
            }

            var reason = transaction.Element(ns + "StsRsnInf");

            items.Add(new ParsedItem(
                endToEndId,
                ToStatus(transaction.Element(ns + "TxSts")?.Value),
                reason?.Element(ns + "Rsn")?.Element(ns + "Cd")?.Value,
                reason?.Elements(ns + "AddtlInf").FirstOrDefault()?.Value
                    ?? reason?.Element(ns + "Rsn")?.Element(ns + "Prtry")?.Value,
                ParseAmount(transaction
                    .Element(ns + "OrgnlTxRef")?.Element(ns + "Amt")?
                    .Element(ns + "InstdAmt")?.Value)));
        }

        return new ParsedReport(
            messageId,
            originalMessageId,
            root.Name.NamespaceName is { Length: > 0 } urn
                ? urn.Split(':').LastOrDefault() ?? "pain.002"
                : "pain.002",
            ParseDate(header?.Element(ns + "CreDtTm")?.Value),
            ToNullableStatus(original?.Element(ns + "GrpSts")?.Value),
            items);
    }

    /// <summary>
    /// ISO 20022 external status codes. Unrecognised maps to Unknown rather than
    /// throwing: a code this parser has not seen is information, and discarding
    /// the row would lose the payment it referred to.
    /// </summary>
    private static BankTransactionStatus ToStatus(string? code) => code?.ToUpperInvariant() switch
    {
        "ACTC" or "ACCP" or "ACWC" => BankTransactionStatus.Accepted,
        "ACSC" or "ACSP" => BankTransactionStatus.Settled,
        "PDNG" => BankTransactionStatus.Pending,
        "RJCT" => BankTransactionStatus.Rejected,
        _ => BankTransactionStatus.Unknown,
    };

    private static BankTransactionStatus? ToNullableStatus(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : ToStatus(code);

    private static decimal? ParseAmount(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static DateTime? ParseDate(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
}

// ------------------------------------------------------------------ handlers

public sealed class ImportPaymentStatusHandler(
    S4herpDbContext db,
    IAuthorizationEnforcer authorization,
    IUserContext user,
    ITenantContext tenant,
    IClock clock,
    ICorrelationContext correlation)
    : ICommandHandler<ImportPaymentStatusCommand, PaymentStatusReportResult>
{
    public async Task<PaymentStatusReportResult> HandleAsync(
        ImportPaymentStatusCommand command, CancellationToken ct)
    {
        var parsed = PaymentStatusReader.Parse(command.Content);

        // The pain.001 this answers. Refusing an unknown original is the point:
        // a status report naming a message we never sent is either somebody
        // else's bank traffic or a forgery, and filing it against our payments
        // would be worse than losing it.
        var file = await db.Set<PaymentFile>()
            .Include(f => f.PaymentRun).ThenInclude(r => r.Items)
            .SingleOrDefaultAsync(f => f.MessageId == parsed.OriginalMessageId, ct)
            ?? throw new BusinessRuleException(
                PaymentStatusErrors.UnknownOriginalMessage,
                $"The report answers message {parsed.OriginalMessageId}, which this system " +
                "did not send. Nothing has been imported.");

        var companyCode = await db.Set<CompanyCode>()
            .SingleAsync(c => c.Id == file.PaymentRun.CompanyCodeId, ct);

        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", companyCode.Code), ("ACTVT", "01")], ct);

        // Re-importing is a no-op, not a duplicate. Bank files are re-sent by mail
        // servers, retried by operators, and picked up twice by anything that
        // polls — and a second set of verdicts on the same payments would double
        // the rejection count and make the outstanding list wrong.
        var existing = await db.Set<PaymentStatusReport>()
            .AsNoTracking()
            .Include(r => r.Items)
            .SingleOrDefaultAsync(r => r.MessageId == parsed.MessageId, ct);

        if (existing is not null)
        {
            return Project(existing, file.PaymentRun.RunId, companyCode.Code, alreadyImported: true);
        }

        var now = clock.UtcNow;

        var report = new PaymentStatusReport
        {
            TenantId = tenant.TenantId,
            MessageId = parsed.MessageId,
            OriginalMessageId = parsed.OriginalMessageId,
            PaymentFileId = file.Id,
            Format = parsed.Format,
            GroupStatus = parsed.GroupStatus,
            Content = command.Content,
            ContentSha256 = Sha256(command.Content),
            ReportCreatedAtUtc = parsed.CreatedAtUtc,
            ImportedAtUtc = now,
            ImportedBy = user.UserName,
            CreatedBy = user.UserName,
        };

        // EndToEndId is {companyCode}-{year}-{paymentDocument} — set that way in
        // increment 8 precisely so the return leg could be matched. Resolving it
        // from the run's own items rather than by re-parsing the string means a
        // change to the format breaks loudly here instead of silently matching
        // nothing.
        var byEndToEndId = file.PaymentRun.Items
            .Where(i => !i.IsExcluded && i.PaymentDocumentNumber is not null)
            .GroupBy(i => EndToEndIdOf(companyCode.Code, file.PaymentRun.RunDate.Year, i))
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var item in parsed.Items)
        {
            var matched = byEndToEndId.GetValueOrDefault(item.EndToEndId);

            report.Items.Add(new PaymentStatusItem
            {
                TenantId = tenant.TenantId,
                EndToEndId = item.EndToEndId,
                Status = item.Status,
                ReasonCode = item.ReasonCode,
                ReasonText = Truncate(item.ReasonText, 400),
                Amount = item.Amount,
                PaymentDocumentNumber = matched?.PaymentDocumentNumber,
                FiscalYear = matched?.FiscalYear,
                CompanyCodeId = matched is null ? null : companyCode.Id,
                CreatedBy = user.UserName,
            });
        }

        db.Add(report);

        var rejected = report.Items.Count(i => i.Status == BankTransactionStatus.Rejected);

        db.Add(new Audit.Domain.AuditLog
        {
            TenantId = tenant.TenantId,
            OccurredAtUtc = now,
            UserName = user.UserName,
            CompanyCodeId = companyCode.Id,
            Action = Audit.Domain.AuditAction.Create,
            ObjectType = "PaymentStatusReport",
            ObjectId = parsed.MessageId,
            SourceApi = "POST /api/v1/finance/payment-status-reports",
            TransactionCode = "F110",
            CorrelationId = correlation.CorrelationId,
            Summary = $"Imported {parsed.Format} for {parsed.OriginalMessageId}: " +
                      $"{report.Items.Count} transactions, {rejected} rejected",
        });

        return Project(report, file.PaymentRun.RunId, companyCode.Code, alreadyImported: false);
    }

    internal static string EndToEndIdOf(string companyCode, int year, PaymentRunItem item) =>
        $"{companyCode}-{year}-{item.PaymentDocumentNumber}";

    private static string? Truncate(string? value, int max) =>
        value is { Length: > 0 } && value.Length > max ? value[..max] : value;

    private static string Sha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    internal static PaymentStatusReportResult Project(
        PaymentStatusReport report, string runId, string companyCode, bool alreadyImported) =>
        new()
        {
            MessageId = report.MessageId,
            OriginalMessageId = report.OriginalMessageId,
            RunId = runId,
            Format = report.Format,
            GroupStatus = report.GroupStatus?.ToString(),
            ImportedAtUtc = report.ImportedAtUtc,
            ImportedBy = report.ImportedBy,
            Accepted = report.Items.Count(i =>
                i.Status is BankTransactionStatus.Accepted or BankTransactionStatus.Settled),
            Rejected = report.Items.Count(i => i.Status == BankTransactionStatus.Rejected),
            Pending = report.Items.Count(i => i.Status == BankTransactionStatus.Pending),
            Unmatched = report.Items.Count(i => i.PaymentDocumentNumber is null),
            AlreadyImported = alreadyImported,
            Items = report.Items
                .OrderBy(i => i.EndToEndId)
                .Select(i => new PaymentStatusItemView(
                    i.EndToEndId,
                    i.Status.ToString(),
                    i.ReasonCode,
                    i.ReasonText,
                    i.Amount,
                    i.CompanyCodeId is null ? null : companyCode,
                    i.FiscalYear,
                    i.PaymentDocumentNumber,
                    i.IsResolved,
                    i.ReversalDocumentNumber))
                .ToList(),
        };
}

/// <summary>
/// Reverses a refused payment and reopens what it cleared. Composed from the two
/// operations that already exist — reset clearing (FBRA) and reverse (FB08) —
/// rather than reimplemented, because "what reversing a payment means" must not
/// have two definitions that can drift apart.
/// </summary>
public sealed class ResolveRejectedPaymentHandler(
    S4herpDbContext db,
    IAuthorizationEnforcer authorization,
    ICommandHandler<ResetClearingCommand, ResetClearingResult> resetClearing,
    ICommandHandler<ReverseJournalEntryCommand, ReverseJournalEntryResult> reverse,
    IUserContext user,
    ITenantContext tenant,
    IClock clock,
    ICorrelationContext correlation)
    : ICommandHandler<ResolveRejectedPaymentCommand, ResolveRejectedPaymentResult>
{
    public async Task<ResolveRejectedPaymentResult> HandleAsync(
        ResolveRejectedPaymentCommand command, CancellationToken ct)
    {
        var item = await db.Set<PaymentStatusItem>()
            .Include(i => i.CompanyCode)
            .Where(i => i.EndToEndId == command.EndToEndId)
            .OrderByDescending(i => i.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(
                $"No bank status has been reported for {command.EndToEndId}.");

        if (item.Status != BankTransactionStatus.Rejected)
        {
            throw new BusinessRuleException(
                PaymentStatusErrors.NotRejected,
                $"The bank reported {command.EndToEndId} as {item.Status}. Only a rejected " +
                "payment is reversed on the bank's say-so.");
        }

        if (item.IsResolved)
        {
            throw new BusinessRuleException(
                PaymentStatusErrors.AlreadyResolved,
                $"{command.EndToEndId} was already reversed by document " +
                $"{item.ReversalDocumentNumber}.");
        }

        if (item.PaymentDocumentNumber is not { } documentNumber
            || item.FiscalYear is not { } fiscalYear
            || item.CompanyCode is null)
        {
            // The bank rejected something this system cannot place. Reversing
            // "whatever that probably was" is precisely the guess a ledger must
            // never make.
            throw new BusinessRuleException(
                PaymentStatusErrors.Unmatched,
                $"{command.EndToEndId} was not matched to a payment document, so there is " +
                "nothing to reverse. Investigate before acting.");
        }

        var companyCode = item.CompanyCode.Code;

        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", companyCode), ("ACTVT", "01")], ct);

        // Order matters and is not arbitrary: an invoice cleared by this payment
        // must be released before the payment document can be reversed, or the
        // reversal leaves the open item pointing at a document that no longer
        // exists.
        var reopened = await resetClearing.HandleAsync(new ResetClearingCommand
        {
            CompanyCode = companyCode,
            FiscalYear = fiscalYear,
            ClearingDocumentNumber = documentNumber,
        }, ct);

        var reversal = await reverse.HandleAsync(new ReverseJournalEntryCommand
        {
            CompanyCode = companyCode,
            FiscalYear = fiscalYear,
            DocumentNumber = documentNumber,
            // 01 is the seeded "reversal in current period" reason. The bank's own
            // words go on the audit trail below, where the length is not capped
            // at a two-character code.
            ReversalReasonCode = "01",
        }, ct);

        var now = clock.UtcNow;
        item.IsResolved = true;
        item.ResolvedAtUtc = now;
        item.ResolvedBy = user.UserName;
        item.ReversalDocumentNumber = reversal.ReversalDocumentNumber;
        item.ModifiedBy = user.UserName;
        item.ModifiedAtUtc = now;

        db.Add(new Audit.Domain.AuditLog
        {
            TenantId = tenant.TenantId,
            OccurredAtUtc = now,
            UserName = user.UserName,
            CompanyCodeId = item.CompanyCodeId,
            Action = Audit.Domain.AuditAction.Reverse,
            ObjectType = "PaymentStatusItem",
            ObjectId = command.EndToEndId,
            SourceApi = "POST /api/v1/finance/payment-status-reports/rejections/{endToEndId}/resolve",
            TransactionCode = "FBRA",
            CorrelationId = correlation.CorrelationId,
            Summary = $"Payment {documentNumber} reversed by {reversal.ReversalDocumentNumber} " +
                      $"after bank rejection {item.ReasonCode}: {item.ReasonText}. " +
                      $"{reopened.ItemsReopened} item(s) reopened. {command.Comment}".Trim(),
        });

        return new ResolveRejectedPaymentResult
        {
            EndToEndId = command.EndToEndId,
            CompanyCode = companyCode,
            PaymentDocumentNumber = documentNumber,
            ReversalDocumentNumber = reversal.ReversalDocumentNumber,
            ItemsReopened = reopened.ItemsReopened,
        };
    }
}

public sealed class GetPaymentStatusReportQueryHandler(
    S4herpDbContext db, IAuthorizationEnforcer authorization)
    : IQueryHandler<GetPaymentStatusReportQuery, PaymentStatusReportResult>
{
    public async Task<PaymentStatusReportResult> HandleAsync(
        GetPaymentStatusReportQuery query, CancellationToken ct)
    {
        var report = await db.Set<PaymentStatusReport>()
            .AsNoTracking()
            .Include(r => r.Items)
            .Include(r => r.PaymentFile).ThenInclude(f => f.PaymentRun)
            .SingleOrDefaultAsync(r => r.MessageId == query.MessageId, ct)
            ?? throw new NotFoundException(
                $"No payment status report {query.MessageId} has been imported.");

        var companyCode = await db.Set<CompanyCode>()
            .AsNoTracking()
            .Where(c => c.Id == report.PaymentFile.PaymentRun.CompanyCodeId)
            .Select(c => c.Code)
            .SingleAsync(ct);

        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", companyCode), ("ACTVT", "03")], ct);

        return ImportPaymentStatusHandler.Project(
            report, report.PaymentFile.PaymentRun.RunId, companyCode, alreadyImported: true);
    }
}

public sealed class GetOutstandingRejectionsQueryHandler(
    S4herpDbContext db, IAuthorizationEnforcer authorization)
    : IQueryHandler<GetOutstandingRejectionsQuery, IReadOnlyList<RejectedPaymentView>>
{
    public async Task<IReadOnlyList<RejectedPaymentView>> HandleAsync(
        GetOutstandingRejectionsQuery query, CancellationToken ct)
    {
        var companyCodes = await authorization.AuthorizedCompanyCodeIdsAsync(ct);

        var items = db.Set<PaymentStatusItem>()
            .AsNoTracking()
            .Include(i => i.CompanyCode)
            .Include(i => i.PaymentStatusReport).ThenInclude(r => r.PaymentFile).ThenInclude(f => f.PaymentRun)
            .Where(i => i.Status == BankTransactionStatus.Rejected
                        && (i.CompanyCodeId == null || companyCodes.Contains(i.CompanyCodeId.Value)));

        if (!query.IncludeResolved)
        {
            items = items.Where(i => !i.IsResolved);
        }

        if (query.CompanyCode is { Length: > 0 } code)
        {
            items = items.Where(i => i.CompanyCode!.Code == code);
        }

        return await items
            .OrderBy(i => i.IsResolved).ThenByDescending(i => i.PaymentStatusReport.ImportedAtUtc)
            .Select(i => new RejectedPaymentView(
                i.EndToEndId,
                i.PaymentStatusReport.MessageId,
                i.PaymentStatusReport.PaymentFile.PaymentRun.RunId,
                i.ReasonCode,
                i.ReasonText,
                i.Amount,
                i.CompanyCode!.Code,
                i.FiscalYear,
                i.PaymentDocumentNumber,
                i.PaymentStatusReport.ImportedAtUtc,
                i.IsResolved))
            .ToListAsync(ct);
    }
}


public sealed class GetRunBankStatusQueryHandler(
    S4herpDbContext db, IAuthorizationEnforcer authorization)
    : IQueryHandler<GetRunBankStatusQuery, RunBankStatusResult>
{
    public async Task<RunBankStatusResult> HandleAsync(
        GetRunBankStatusQuery query, CancellationToken ct)
    {
        var run = await db.Set<PaymentRun>()
            .AsNoTracking()
            .Include(r => r.CompanyCode)
            .SingleOrDefaultAsync(r => r.RunId == query.RunId, ct)
            ?? throw new NotFoundException($"Payment run {query.RunId} does not exist.");

        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", run.CompanyCode.Code), ("ACTVT", "03")], ct);

        var items = await db.Set<PaymentStatusItem>()
            .AsNoTracking()
            .Include(i => i.PaymentStatusReport)
            .Where(i => i.PaymentStatusReport.PaymentFile.PaymentRun.RunId == query.RunId)
            .ToListAsync(ct);

        // Latest word per transaction. Ordering by import time and not by the
        // report's own creation date: a bank that back-dates a correction is
        // still telling you something later than what it told you before.
        var latest = items
            .GroupBy(i => i.EndToEndId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g
                .OrderByDescending(i => i.PaymentStatusReport.ImportedAtUtc)
                .ThenByDescending(i => i.Id)
                .First())
            .OrderBy(i => i.EndToEndId)
            .ToList();

        return new RunBankStatusResult
        {
            RunId = query.RunId,
            Accepted = latest.Count(i =>
                i.Status is BankTransactionStatus.Accepted or BankTransactionStatus.Settled),
            Rejected = latest.Count(i => i.Status == BankTransactionStatus.Rejected),
            Pending = latest.Count(i => i.Status == BankTransactionStatus.Pending),
            Unmatched = latest.Count(i => i.PaymentDocumentNumber is null),
            Items = latest
                .Select(i => new PaymentStatusItemView(
                    i.EndToEndId,
                    i.Status.ToString(),
                    i.ReasonCode,
                    i.ReasonText,
                    i.Amount,
                    run.CompanyCode.Code,
                    i.FiscalYear,
                    i.PaymentDocumentNumber,
                    i.IsResolved,
                    i.ReversalDocumentNumber))
                .ToList(),
        };
    }
}
