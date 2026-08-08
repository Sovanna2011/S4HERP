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
/// Turns an executed run into the instruction the bank actually acts on: an
/// ISO 20022 pain.001 customer credit transfer initiation.
/// </summary>
[RequiresAuthorization("F_BKPF_BUK", "01")]
public sealed record GeneratePaymentFileCommand : ICommand<PaymentFileResult>
{
    public required string RunId { get; init; }
}

[RequiresAuthorization("F_BKPF_BUK", "03")]
public sealed record GetPaymentFileQuery : IQuery<PaymentFileResult>
{
    public required string RunId { get; init; }
}

/// <summary>
/// Hands over the XML itself. A command rather than a query because it records
/// who took a copy — for a file that authorises money to move, "who has it" is
/// as much a fact worth keeping as "what is in it".
/// </summary>
[RequiresAuthorization("F_BKPF_BUK", "03")]
public sealed record DownloadPaymentFileCommand : ICommand<PaymentFileDownload>
{
    public required string RunId { get; init; }
}

/// <summary>
/// Metadata only. Deliberately carries no account number: the file is the one
/// place those appear in the clear, and it is behind S_EXPORT.
/// </summary>
public sealed record PaymentFileResult
{
    public required string RunId { get; init; }
    public required string MessageId { get; init; }
    public required string Format { get; init; }
    public required string FileName { get; init; }
    public required int TransactionCount { get; init; }
    public required decimal ControlSum { get; init; }
    public required string Currency { get; init; }
    public required string ContentSha256 { get; init; }
    public required DateTime GeneratedAtUtc { get; init; }
    public required string GeneratedBy { get; init; }
    public required int DownloadCount { get; init; }
    public DateTime? FirstDownloadedAtUtc { get; init; }
    public string? FirstDownloadedBy { get; init; }
}

public sealed record PaymentFileDownload
{
    public required string FileName { get; init; }
    public required string Content { get; init; }
    public required string ContentSha256 { get; init; }
}

internal static class PaymentFileErrors
{
    public const string RunNotExecuted = "PAYMENT_RUN_NOT_EXECUTED";
    public const string AlreadyGenerated = "PAYMENT_FILE_ALREADY_GENERATED";
    public const string NotGenerated = "PAYMENT_FILE_NOT_GENERATED";
    public const string PartnerBankMissing = "PARTNER_BANK_DETAILS_MISSING";
}

// ---------------------------------------------------------------- generation

public sealed class GeneratePaymentFileHandler(
    S4herpDbContext db,
    IAuthorizationEnforcer authorization,
    IUserContext user,
    ITenantContext tenant,
    IClock clock,
    ICorrelationContext correlation)
    : ICommandHandler<GeneratePaymentFileCommand, PaymentFileResult>
{
    public async Task<PaymentFileResult> HandleAsync(
        GeneratePaymentFileCommand command, CancellationToken ct)
    {
        var run = await ExecutePaymentRunHandler.LoadAsync(db, command.RunId, ct);

        var companyCode = await db.Set<CompanyCode>()
            .SingleAsync(c => c.Id == run.CompanyCodeId, ct);

        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", companyCode.Code), ("ACTVT", "01")], ct);

        if (run.Status != PaymentRunStatus.Executed)
        {
            // A file for a proposal would be an instruction to pay money the
            // ledger does not yet say was paid. Execution first, always.
            throw new BusinessRuleException(
                PaymentFileErrors.RunNotExecuted,
                $"Payment run {run.RunId} is {run.Status}. Only an executed run has payments to instruct.");
        }

        var existing = await db.Set<PaymentFile>()
            .SingleOrDefaultAsync(f => f.PaymentRunId == run.Id, ct);

        if (existing is not null)
        {
            throw new BusinessRuleException(
                PaymentFileErrors.AlreadyGenerated,
                $"Payment run {run.RunId} already has payment file {existing.MessageId}. " +
                "Download that one; a second file is a second instruction to the bank.");
        }

        var account = await db.Set<HouseBankAccount>()
            .Include(a => a.HouseBank)
            .SingleAsync(a => a.Id == run.HouseBankAccountId, ct);

        var currency = await db.Set<Currency>()
            .Where(c => c.Id == account.CurrencyId).Select(c => c.Code).SingleAsync(ct);

        var payments = await LoadPaymentsAsync(run, companyCode.Code, ct);

        var generatedAt = clock.UtcNow;
        var messageId = MessageId(companyCode.Code, run.RunId, generatedAt);

        var xml = PaymentFileWriter.Write(new PaymentFileInput
        {
            MessageId = messageId,
            CreatedAtUtc = generatedAt,
            RunId = run.RunId,
            ExecutionDate = run.RunDate,
            Currency = currency,
            DebtorName = companyCode.Name,
            DebtorIban = account.Iban,
            DebtorAccountNumber = account.AccountNumber,
            DebtorBic = account.HouseBank.SwiftCode,
            Payments = payments,
        });

        var file = new PaymentFile
        {
            TenantId = tenant.TenantId,
            PaymentRunId = run.Id,
            MessageId = messageId,
            Format = PaymentFileWriter.Format,
            Content = xml,
            ContentSha256 = Sha256(xml),
            TransactionCount = payments.Count,
            ControlSum = payments.Sum(p => p.Amount),
            CurrencyId = account.CurrencyId,
            GeneratedAtUtc = generatedAt,
            GeneratedBy = user.UserName,
            CreatedBy = user.UserName,
        };
        db.Add(file);

        db.Add(new Audit.Domain.AuditLog
        {
            TenantId = tenant.TenantId,
            OccurredAtUtc = generatedAt,
            UserName = user.UserName,
            CompanyCodeId = run.CompanyCodeId,
            Action = Audit.Domain.AuditAction.Create,
            ObjectType = "PaymentFile",
            ObjectId = run.RunId,
            SourceApi = "POST /api/v1/finance/payment-runs/{runId}/payment-file",
            TransactionCode = "F110",
            CorrelationId = correlation.CorrelationId,
            // Counts and a hash, never an account number. §21 forbids recording
            // bank-account secrets in logs, and an audit row is a log.
            Summary = $"Generated {PaymentFileWriter.Format} {messageId}: " +
                      $"{payments.Count} transaction(s), {file.ControlSum:N2} {currency}, " +
                      $"sha256 {file.ContentSha256[..16]}…",
        });

        return Project(file, run.RunId, currency);
    }

    /// <summary>
    /// One credit transfer per payment document — which is one per partner,
    /// because that is how the run posts. The remittance line names the invoices
    /// it settles, so the supplier can apply the receipt without ringing up.
    /// </summary>
    private async Task<List<PaymentFileTransaction>> LoadPaymentsAsync(
        PaymentRun run, string companyCode, CancellationToken ct)
    {
        var paid = run.Items.Where(i => !i.IsExcluded && i.PaymentDocumentNumber is not null).ToList();

        var partnerIds = paid.Select(i => i.BusinessPartnerId).Distinct().ToList();

        var partners = await db.Set<BusinessPartner.Domain.Partner>()
            .Where(p => partnerIds.Contains(p.Id))
            .Select(p => new { p.Id, p.PartnerNumber, p.Name })
            .ToDictionaryAsync(p => p.Id, ct);

        // The bank details as they stand now, not as they stood when the run was
        // proposed. Deliberate: the file is what the bank will act on today, and
        // an account that has since been replaced must not be paid.
        var onDate = run.RunDate;
        var banks = await db.Set<BusinessPartner.Domain.PartnerBank>()
            .Where(b => partnerIds.Contains(b.PartnerId)
                        && b.ValidFrom <= onDate && b.ValidTo >= onDate)
            .ToListAsync(ct);

        var transactions = new List<PaymentFileTransaction>();

        foreach (var group in paid
            .GroupBy(i => new { i.BusinessPartnerId, DocumentNumber = i.PaymentDocumentNumber!.Value })
            .OrderBy(g => g.Key.DocumentNumber))
        {
            var partner = partners[group.Key.BusinessPartnerId];

            var bank = banks.Where(b => b.PartnerId == group.Key.BusinessPartnerId)
                .OrderByDescending(b => b.IsDefault).ThenBy(b => b.Id).FirstOrDefault()
                ?? throw new BusinessRuleException(
                    PaymentFileErrors.PartnerBankMissing,
                    $"Partner {partner.PartnerNumber} has no valid bank details, so no " +
                    "credit transfer can be instructed. The payment is posted; the file is not.");

            var references = group
                .OrderBy(i => i.FiscalYear).ThenBy(i => i.DocumentNumber)
                .Select(i => $"{i.FiscalYear}/{i.DocumentNumber}");

            transactions.Add(new PaymentFileTransaction
            {
                EndToEndId = $"{companyCode}-{run.RunDate:yyyy}-{group.Key.DocumentNumber}",
                Amount = group.Sum(i => i.Amount),
                // The account holder, not the partner name: they differ often
                // enough (trading names, factoring arrangements) that using the
                // wrong one is how a transfer bounces on a name check. The partner
                // name is only a fallback, and a person has none at all — the
                // partner number is the last resort so the field is never empty.
                CreditorName = Coalesce(bank.AccountHolder, partner.Name, partner.PartnerNumber),
                CreditorIban = bank.Iban,
                CreditorAccountNumber = bank.AccountNumber,
                CreditorBic = bank.Swift,
                RemittanceInformation = Truncate($"Invoices {string.Join(", ", references)}", 140),
            });
        }

        return transactions;
    }

    /// <summary>
    /// ISO 20022 caps <c>MsgId</c> at 35 characters, and the run id alone can be
    /// 40. Company code, the generation instant and a short digest of the run id:
    /// unique in practice, and traceable back to the run by eye.
    /// </summary>
    private static string MessageId(string companyCode, string runId, DateTime generatedAtUtc)
    {
        var digest = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(runId)))[..8];

        return $"{companyCode}-{generatedAtUtc:yyyyMMddHHmmss}-{digest}";
    }

    private static string Sha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static string Coalesce(params string?[] candidates) =>
        candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))
        ?? throw new InvalidOperationException("No creditor name candidate was supplied.");

    internal static PaymentFileResult Project(PaymentFile file, string runId, string currency) =>
        new()
        {
            RunId = runId,
            MessageId = file.MessageId,
            Format = file.Format,
            FileName = FileName(runId, file.MessageId),
            TransactionCount = file.TransactionCount,
            ControlSum = file.ControlSum,
            Currency = currency,
            ContentSha256 = file.ContentSha256,
            GeneratedAtUtc = file.GeneratedAtUtc,
            GeneratedBy = file.GeneratedBy,
            DownloadCount = file.DownloadCount,
            FirstDownloadedAtUtc = file.FirstDownloadedAtUtc,
            FirstDownloadedBy = file.FirstDownloadedBy,
        };

    internal static string FileName(string runId, string messageId) =>
        $"pain001-{runId}-{messageId}.xml";
}

// ------------------------------------------------------------------ download

public sealed class DownloadPaymentFileHandler(
    S4herpDbContext db,
    IAuthorizationEnforcer authorization,
    IUserContext user,
    ITenantContext tenant,
    IClock clock,
    ICorrelationContext correlation)
    : ICommandHandler<DownloadPaymentFileCommand, PaymentFileDownload>
{
    public async Task<PaymentFileDownload> HandleAsync(
        DownloadPaymentFileCommand command, CancellationToken ct)
    {
        var (run, companyCode, file) = await PaymentFileQueries
            .LoadAsync(db, command.RunId, ct);

        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", companyCode.Code), ("ACTVT", "03")], ct);

        // Seeing a payment run is not the same permission as walking out with
        // every creditor's account number. S_EXPORT is the object the blueprint
        // reserves for exactly that, and SOD001 already names bank details plus
        // the payment run as a critical conflict.
        await authorization.RequireAsync(
            "S_EXPORT", [("SCOPE", PaymentFileExport.Scope)], ct);

        var now = clock.UtcNow;

        file.DownloadCount++;
        file.FirstDownloadedAtUtc ??= now;
        file.FirstDownloadedBy ??= user.UserName;
        file.LastDownloadedAtUtc = now;
        file.LastDownloadedBy = user.UserName;

        db.Add(new Audit.Domain.AuditLog
        {
            TenantId = tenant.TenantId,
            OccurredAtUtc = now,
            UserName = user.UserName,
            CompanyCodeId = run.CompanyCodeId,
            Action = Audit.Domain.AuditAction.Export,
            ObjectType = "PaymentFile",
            ObjectId = run.RunId,
            SourceApi = "GET /api/v1/finance/payment-runs/{runId}/payment-file/content",
            TransactionCode = "F110",
            CorrelationId = correlation.CorrelationId,
            Summary = $"Downloaded {file.MessageId} (copy {file.DownloadCount}), " +
                      $"sha256 {file.ContentSha256[..16]}…",
        });

        return new PaymentFileDownload
        {
            FileName = GeneratePaymentFileHandler.FileName(run.RunId, file.MessageId),
            Content = file.Content,
            ContentSha256 = file.ContentSha256,
        };
    }
}

public sealed class GetPaymentFileQueryHandler(
    S4herpDbContext db, IAuthorizationEnforcer authorization)
    : IQueryHandler<GetPaymentFileQuery, PaymentFileResult>
{
    public async Task<PaymentFileResult> HandleAsync(GetPaymentFileQuery query, CancellationToken ct)
    {
        var (run, companyCode, file) = await PaymentFileQueries.LoadAsync(db, query.RunId, ct);

        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", companyCode.Code), ("ACTVT", "03")], ct);

        var currency = await db.Set<Currency>()
            .Where(c => c.Id == file.CurrencyId).Select(c => c.Code).SingleAsync(ct);

        return GeneratePaymentFileHandler.Project(file, run.RunId, currency);
    }
}

internal static class PaymentFileExport
{
    /// <summary>The S_EXPORT SCOPE value that permits taking the XML away.</summary>
    public const string Scope = "PAYMENT_FILE";
}

internal static class PaymentFileQueries
{
    public static async Task<(PaymentRun Run, CompanyCode Company, PaymentFile File)> LoadAsync(
        S4herpDbContext db, string runId, CancellationToken ct)
    {
        var run = await db.Set<PaymentRun>().SingleOrDefaultAsync(r => r.RunId == runId, ct)
            ?? throw new NotFoundException($"Payment run {runId} does not exist.");

        var companyCode = await db.Set<CompanyCode>()
            .SingleAsync(c => c.Id == run.CompanyCodeId, ct);

        var file = await db.Set<PaymentFile>().SingleOrDefaultAsync(f => f.PaymentRunId == run.Id, ct)
            ?? throw new BusinessRuleException(
                PaymentFileErrors.NotGenerated,
                $"Payment run {runId} has no payment file. Generate it first.");

        return (run, companyCode, file);
    }
}

// -------------------------------------------------------------------- writer

public sealed record PaymentFileTransaction
{
    public required string EndToEndId { get; init; }
    public required decimal Amount { get; init; }
    public required string CreditorName { get; init; }
    public string? CreditorIban { get; init; }
    public required string CreditorAccountNumber { get; init; }
    public string? CreditorBic { get; init; }
    public required string RemittanceInformation { get; init; }
}

public sealed record PaymentFileInput
{
    public required string MessageId { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required string RunId { get; init; }
    public required DateOnly ExecutionDate { get; init; }
    public required string Currency { get; init; }
    public required string DebtorName { get; init; }
    public string? DebtorIban { get; init; }
    public required string DebtorAccountNumber { get; init; }
    public string? DebtorBic { get; init; }
    public required IReadOnlyList<PaymentFileTransaction> Payments { get; init; }
}

/// <summary>
/// Writes pain.001.001.09. Built with <see cref="XDocument"/> rather than string
/// concatenation, so a creditor called "Smith &amp; Sons" produces valid XML
/// instead of a file the bank silently drops.
/// </summary>
public static class PaymentFileWriter
{
    public const string Format = "pain.001.001.09";

    private static readonly XNamespace Ns = "urn:iso:std:iso:20022:tech:xsd:pain.001.001.09";

    public static string Write(PaymentFileInput input)
    {
        var controlSum = input.Payments.Sum(p => p.Amount);

        var document = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(Ns + "Document",
                new XElement(Ns + "CstmrCdtTrfInitn",
                    GroupHeader(input, controlSum),
                    PaymentInformation(input, controlSum))));

        // The bank parses bytes, so the encoding in the declaration has to be the
        // encoding of what is written. Saving an XDocument to a string yields
        // UTF-16 unless said otherwise; this is stored as text and served as UTF-8.
        using var writer = new Utf8StringWriter();
        document.Save(writer);
        return writer.ToString();
    }

    private static XElement GroupHeader(PaymentFileInput input, decimal controlSum) =>
        new(Ns + "GrpHdr",
            new XElement(Ns + "MsgId", input.MessageId),
            new XElement(Ns + "CreDtTm", input.CreatedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)),
            new XElement(Ns + "NbOfTxs", input.Payments.Count),
            new XElement(Ns + "CtrlSum", Amount(controlSum)),
            new XElement(Ns + "InitgPty", new XElement(Ns + "Nm", input.DebtorName)));

    private static XElement PaymentInformation(PaymentFileInput input, decimal controlSum)
    {
        var element = new XElement(Ns + "PmtInf",
            new XElement(Ns + "PmtInfId", input.MessageId),
            new XElement(Ns + "PmtMtd", "TRF"),
            // False: the bank books each transfer separately, so the statement
            // shows one line per supplier and the ledger reconciles item by item.
            // Batch booking would collapse them into a total nobody can match.
            new XElement(Ns + "BtchBookg", "false"),
            new XElement(Ns + "NbOfTxs", input.Payments.Count),
            new XElement(Ns + "CtrlSum", Amount(controlSum)),
            new XElement(Ns + "ReqdExctnDt",
                new XElement(Ns + "Dt", input.ExecutionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))),
            new XElement(Ns + "Dbtr", new XElement(Ns + "Nm", input.DebtorName)),
            new XElement(Ns + "DbtrAcct",
                new XElement(Ns + "Id", AccountId(input.DebtorIban, input.DebtorAccountNumber)),
                new XElement(Ns + "Ccy", input.Currency)));

        if (input.DebtorBic is { Length: > 0 } bic)
        {
            element.Add(new XElement(Ns + "DbtrAgt",
                new XElement(Ns + "FinInstnId", new XElement(Ns + "BICFI", bic))));
        }

        // SLEV: charges as agreed in the service level, the ordinary choice for a
        // domestic or SEPA batch. SHAR/DEBT belong to cross-border cases the run
        // does not model, and picking one of those here would quietly change who
        // pays the bank's fee.
        element.Add(new XElement(Ns + "ChrgBr", "SLEV"));

        foreach (var payment in input.Payments)
        {
            element.Add(CreditTransfer(payment, input.Currency));
        }

        return element;
    }

    private static XElement CreditTransfer(PaymentFileTransaction payment, string currency)
    {
        var element = new XElement(Ns + "CdtTrfTxInf",
            new XElement(Ns + "PmtId",
                new XElement(Ns + "InstrId", payment.EndToEndId),
                // What comes back on the bank statement, and therefore what makes
                // the return leg reconcilable to the payment document.
                new XElement(Ns + "EndToEndId", payment.EndToEndId)),
            new XElement(Ns + "Amt",
                new XElement(Ns + "InstdAmt",
                    new XAttribute("Ccy", currency), Amount(payment.Amount))));

        if (payment.CreditorBic is { Length: > 0 } bic)
        {
            element.Add(new XElement(Ns + "CdtrAgt",
                new XElement(Ns + "FinInstnId", new XElement(Ns + "BICFI", bic))));
        }

        element.Add(new XElement(Ns + "Cdtr", new XElement(Ns + "Nm", payment.CreditorName)));
        element.Add(new XElement(Ns + "CdtrAcct",
            new XElement(Ns + "Id", AccountId(payment.CreditorIban, payment.CreditorAccountNumber))));
        element.Add(new XElement(Ns + "RmtInf",
            new XElement(Ns + "Ustrd", payment.RemittanceInformation)));

        return element;
    }

    /// <summary>
    /// IBAN when there is one, <c>Othr/Id</c> when there is not. Not a fallback
    /// for missing data — Cambodian and Thai banks, which is where this system's
    /// company codes are, do not issue IBANs at all. Writing an empty
    /// <c>&lt;IBAN/&gt;</c> would produce a file every one of them rejects.
    /// </summary>
    private static XElement AccountId(string? iban, string accountNumber) =>
        iban is { Length: > 0 }
            ? new XElement(Ns + "IBAN", iban)
            : new XElement(Ns + "Othr", new XElement(Ns + "Id", accountNumber));

    private static string Amount(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
