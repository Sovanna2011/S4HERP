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
/// Imports an ISO 20022 camt.053 bank statement.
///
/// The first thing in this system that describes what the account actually did.
/// Everything from increment 6 onward has been about instructing the bank
/// (pain.001) and hearing back about those instructions (pain.002); neither
/// says what the account holds. A transfer the bank accepted and then returned
/// a week later shows up here and nowhere else.
/// </summary>
[RequiresAuthorization("F_BKPF_BUK", "01")]
public sealed record ImportBankStatementCommand : ICommand<BankStatementResult>
{
    /// <summary>The camt.053 document, as the bank sent it.</summary>
    public required string Content { get; init; }
}

/// <summary>
/// Ties a statement line to a posted document by hand, where automatic matching
/// could not. Records the correspondence; changes neither side.
/// </summary>
[RequiresAuthorization("F_BKPF_BUK", "01")]
public sealed record MatchStatementLineCommand : ICommand<BankStatementLineView>
{
    /// <summary>From the route. Never <c>required</c> — see ResolveRejectedPaymentCommand.</summary>
    public string StatementId { get; init; } = string.Empty;
    public int LineNumber { get; init; }

    public required short FiscalYear { get; init; }
    public required long DocumentNumber { get; init; }
    public string? Comment { get; init; }
}

/// <summary>
/// Marks a line as needing no document of ours — bank charges, interest, a
/// transfer between our own accounts.
///
/// Distinct from matched, and deliberately so: "somebody looked and decided this
/// needs nothing" is a different fact from "this corresponds to document 123",
/// and a reconciliation that conflates them cannot be reviewed.
/// </summary>
[RequiresAuthorization("F_BKPF_BUK", "01")]
public sealed record IgnoreStatementLineCommand : ICommand<BankStatementLineView>
{
    public string StatementId { get; init; } = string.Empty;
    public int LineNumber { get; init; }

    /// <summary>Mandatory. An unexplained dismissal is indistinguishable from an oversight.</summary>
    public required string Reason { get; init; }
}

/// <summary>Body of a match or ignore request; carries no route fields.</summary>
public sealed record MatchLineBody
{
    public short FiscalYear { get; init; }
    public long DocumentNumber { get; init; }
    public string? Comment { get; init; }
    public string? Reason { get; init; }
}

[RequiresAuthorization("F_BKPF_BUK", "03")]
public sealed record GetBankStatementQuery : IQuery<BankStatementResult>
{
    public required string StatementId { get; init; }
}

[RequiresAuthorization("F_BKPF_BUK", "03")]
public sealed record ListBankStatementsQuery : IQuery<IReadOnlyList<BankStatementSummary>>
{
    public string? CompanyCode { get; init; }
    public int Take { get; init; } = 50;
}

/// <summary>
/// The reconciliation question: does the bank agree with the ledger, and if not,
/// what is outstanding. This is what a month-end close is blocked on.
/// </summary>
[RequiresAuthorization("F_BKPF_BUK", "03")]
public sealed record GetBankReconciliationQuery : IQuery<BankReconciliationResult>
{
    public required string StatementId { get; init; }
}

// ------------------------------------------------------------------ results

public sealed record BankStatementResult
{
    public required string StatementId { get; init; }
    public required string CompanyCode { get; init; }
    public required string HouseBankAccount { get; init; }
    public required string Format { get; init; }
    public required DateOnly StatementDate { get; init; }
    public required decimal OpeningBalance { get; init; }
    public required decimal ClosingBalance { get; init; }
    public required string Currency { get; init; }
    public required DateTime ImportedAtUtc { get; init; }
    public required string ImportedBy { get; init; }

    public required int LineCount { get; init; }
    public required int Matched { get; init; }
    public required int Unmatched { get; init; }
    public required int Ignored { get; init; }

    public required bool AlreadyImported { get; init; }
    public required IReadOnlyList<BankStatementLineView> Lines { get; init; }
}

public sealed record BankStatementLineView(
    int LineNumber,
    string? EntryReference,
    decimal Amount,
    bool IsCredit,
    DateOnly? BookingDate,
    DateOnly? ValueDate,
    string? EndToEndId,
    string? RemittanceInformation,
    string? CounterpartyName,
    string Status,
    short? FiscalYear,
    long? DocumentNumber,
    string? MatchMethod,
    string? MatchedBy,
    string? MatchComment);

public sealed record BankStatementSummary(
    string StatementId,
    string CompanyCode,
    string HouseBankAccount,
    DateOnly StatementDate,
    decimal ClosingBalance,
    string Currency,
    int LineCount,
    int Unmatched);

/// <summary>
/// Statement closing balance against the G/L bank account, and what stands
/// between them.
/// </summary>
public sealed record BankReconciliationResult
{
    public required string StatementId { get; init; }
    public required string CompanyCode { get; init; }
    public required string HouseBankAccount { get; init; }
    public required string GLAccount { get; init; }
    public required DateOnly StatementDate { get; init; }
    public required string Currency { get; init; }

    /// <summary>What the bank says the account holds.</summary>
    public required decimal StatementClosingBalance { get; init; }

    /// <summary>What the ledger says it holds, on the same date.</summary>
    public required decimal LedgerBalance { get; init; }

    /// <summary>
    /// Bank minus ledger. Zero means reconciled; anything else is the amount
    /// somebody has to explain, and the unreconciled lines below are the
    /// candidates.
    /// </summary>
    public required decimal Difference { get; init; }

    public required bool IsReconciled { get; init; }

    /// <summary>Statement movements with no counterpart in the ledger.</summary>
    public required IReadOnlyList<BankStatementLineView> UnmatchedLines { get; init; }
    public required decimal UnmatchedTotal { get; init; }
}

internal static class BankStatementErrors
{
    public const string Malformed = "BANK_STATEMENT_MALFORMED";
    public const string UnknownAccount = "BANK_STATEMENT_UNKNOWN_ACCOUNT";
    public const string LineNotFound = "BANK_STATEMENT_LINE_NOT_FOUND";
    public const string AlreadyMatched = "BANK_STATEMENT_LINE_ALREADY_MATCHED";
    public const string DocumentNotFound = "BANK_STATEMENT_DOCUMENT_NOT_FOUND";
    public const string AmountMismatch = "BANK_STATEMENT_AMOUNT_MISMATCH";
}

// ------------------------------------------------------------------- parsing

/// <summary>
/// camt.053 reader. Namespace-agnostic for the same reason the pain.002 reader
/// is: banks send .02, .08 and newer, and the elements read here are stable
/// across all of them.
/// </summary>
internal static class BankStatementReader
{
    internal sealed record ParsedStatement(
        string StatementId,
        int? LegalSequenceNumber,
        string Format,
        string? Iban,
        string? AccountOther,
        string? CurrencyCode,
        DateOnly StatementDate,
        DateOnly? FromDate,
        decimal OpeningBalance,
        decimal ClosingBalance,
        IReadOnlyList<ParsedEntry> Entries);

    internal sealed record ParsedEntry(
        string? EntryReference,
        decimal Amount,
        bool IsCredit,
        DateOnly? BookingDate,
        DateOnly? ValueDate,
        string? EndToEndId,
        string? RemittanceInformation,
        string? CounterpartyName,
        string? BankTransactionCode);

    public static ParsedStatement Parse(string content)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(content);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new BusinessRuleException(
                BankStatementErrors.Malformed, $"The file is not well-formed XML: {ex.Message}");
        }

        var root = document.Root
            ?? throw new BusinessRuleException(
                BankStatementErrors.Malformed, "The file has no root element.");

        var ns = root.GetDefaultNamespace();
        var statement = root.Element(ns + "BkToCstmrStmt")?.Element(ns + "Stmt")
            ?? throw new BusinessRuleException(
                BankStatementErrors.Malformed,
                "The file is not a bank-to-customer statement: no BkToCstmrStmt/Stmt element.");

        var statementId = statement.Element(ns + "Id")?.Value;
        if (string.IsNullOrWhiteSpace(statementId))
        {
            throw new BusinessRuleException(
                BankStatementErrors.Malformed, "The statement has no Stmt/Id.");
        }

        var account = statement.Element(ns + "Acct");
        var accountId = account?.Element(ns + "Id");

        // Opening and closing per the standard's balance codes. OPBD/PRCD open,
        // CLBD closes; a bank that sends only interim balances leaves these at
        // zero rather than having them invented from the entries.
        var (opening, closing) = ReadBalances(statement, ns);

        var entries = new List<ParsedEntry>();
        foreach (var entry in statement.Elements(ns + "Ntry"))
        {
            var amount = ParseAmount(entry.Element(ns + "Amt")?.Value);
            if (amount is null)
            {
                // An entry without an amount is not a movement. Skipping it keeps
                // the balance arithmetic honest, and the line count will not
                // match the file — which is the visible symptom of a bad file.
                continue;
            }

            // Transaction details sit one or two levels down depending on whether
            // the bank batches; Descendants spares us caring which.
            var details = entry.Descendants(ns + "TxDtls").FirstOrDefault();

            entries.Add(new ParsedEntry(
                entry.Element(ns + "NtryRef")?.Value
                    ?? entry.Element(ns + "AcctSvcrRef")?.Value,
                amount.Value,
                string.Equals(entry.Element(ns + "CdtDbtInd")?.Value, "CRDT",
                    StringComparison.OrdinalIgnoreCase),
                ParseDate(entry.Element(ns + "BookgDt")?.Element(ns + "Dt")?.Value),
                ParseDate(entry.Element(ns + "ValDt")?.Element(ns + "Dt")?.Value),
                details?.Element(ns + "Refs")?.Element(ns + "EndToEndId")?.Value,
                details?.Element(ns + "RmtInf")?.Element(ns + "Ustrd")?.Value
                    ?? entry.Element(ns + "AddtlNtryInf")?.Value,
                CounterpartyOf(details, ns),
                entry.Element(ns + "BkTxCd")?.Element(ns + "Domn")?.Element(ns + "Cd")?.Value));
        }

        return new ParsedStatement(
            statementId,
            int.TryParse(statement.Element(ns + "LglSeqNb")?.Value, out var seq) ? seq : null,
            root.Name.NamespaceName is { Length: > 0 } urn
                ? urn.Split(':').LastOrDefault() ?? "camt.053"
                : "camt.053",
            accountId?.Element(ns + "IBAN")?.Value,
            accountId?.Element(ns + "Othr")?.Element(ns + "Id")?.Value,
            account?.Element(ns + "Ccy")?.Value,
            ParseDate(statement.Element(ns + "CreDtTm")?.Value?[..10])
                ?? ParseDate(statement.Element(ns + "FrToDt")?.Element(ns + "ToDtTm")?.Value?[..10])
                ?? DateOnly.MinValue,
            ParseDate(statement.Element(ns + "FrToDt")?.Element(ns + "FrDtTm")?.Value?[..10]),
            opening,
            closing,
            entries);
    }

    private static (decimal Opening, decimal Closing) ReadBalances(XElement statement, XNamespace ns)
    {
        decimal opening = 0, closing = 0;

        foreach (var balance in statement.Elements(ns + "Bal"))
        {
            var code = balance.Element(ns + "Tp")?.Element(ns + "CdOrPrtry")?.Element(ns + "Cd")?.Value;
            var amount = ParseAmount(balance.Element(ns + "Amt")?.Value) ?? 0m;

            // A debit balance on our own account means an overdraft, and the sign
            // has to survive: a statement showing 500 overdrawn is not the same
            // fact as one showing 500 in hand.
            var signed = string.Equals(balance.Element(ns + "CdtDbtInd")?.Value, "DBIT",
                StringComparison.OrdinalIgnoreCase) ? -amount : amount;

            switch (code?.ToUpperInvariant())
            {
                case "OPBD" or "PRCD":
                    opening = signed;
                    break;
                case "CLBD":
                    closing = signed;
                    break;
            }
        }

        return (opening, closing);
    }

    private static string? CounterpartyOf(XElement? details, XNamespace ns)
    {
        var parties = details?.Element(ns + "RltdPties");
        return parties?.Element(ns + "Cdtr")?.Element(ns + "Nm")?.Value
            ?? parties?.Element(ns + "Cdtr")?.Element(ns + "Pty")?.Element(ns + "Nm")?.Value
            ?? parties?.Element(ns + "Dbtr")?.Element(ns + "Nm")?.Value
            ?? parties?.Element(ns + "Dbtr")?.Element(ns + "Pty")?.Element(ns + "Nm")?.Value;
    }

    private static decimal? ParseAmount(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
}

// ------------------------------------------------------------------ handlers

public sealed class ImportBankStatementHandler(
    S4herpDbContext db,
    IAuthorizationEnforcer authorization,
    IUserContext user,
    ITenantContext tenant,
    IClock clock,
    ICorrelationContext correlation)
    : ICommandHandler<ImportBankStatementCommand, BankStatementResult>
{
    public async Task<BankStatementResult> HandleAsync(
        ImportBankStatementCommand command, CancellationToken ct)
    {
        var parsed = BankStatementReader.Parse(command.Content);

        // Which of our accounts this is. Matched on IBAN or the account number,
        // because Cambodian banks issue no IBAN — the same reason the outgoing
        // file writes Othr/Id (increment 8).
        var account = await ResolveAccountAsync(parsed, ct);

        var companyCode = await db.Set<CompanyCode>()
            .SingleAsync(c => c.Id == account.HouseBank.CompanyCodeId, ct);

        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", companyCode.Code), ("ACTVT", "01")], ct);

        var existing = await db.Set<BankStatement>()
            .AsNoTracking()
            .Include(s => s.Lines)
            .SingleOrDefaultAsync(s => s.StatementId == parsed.StatementId, ct);

        if (existing is not null)
        {
            // Re-importing a statement would double every movement on the
            // account. Same reasoning as the pain.002, and worse consequences.
            return Project(existing, companyCode.Code,
                $"{account.HouseBank.Code}/{account.Code}",
                await CurrencyCodeAsync(existing.CurrencyId, ct),
                alreadyImported: true);
        }

        var now = clock.UtcNow;
        var statement = new BankStatement
        {
            TenantId = tenant.TenantId,
            StatementId = parsed.StatementId,
            LegalSequenceNumber = parsed.LegalSequenceNumber,
            HouseBankAccountId = account.Id,
            CompanyCodeId = companyCode.Id,
            Format = parsed.Format,
            OpeningBalance = parsed.OpeningBalance,
            ClosingBalance = parsed.ClosingBalance,
            CurrencyId = account.CurrencyId,
            StatementDate = parsed.StatementDate,
            FromDate = parsed.FromDate,
            Content = command.Content,
            ContentSha256 = Sha256(command.Content),
            ImportedAtUtc = now,
            ImportedBy = user.UserName,
            CreatedBy = user.UserName,
        };

        // Automatic matching, on the identifier this system itself put on the
        // outgoing instruction. Nothing else is attempted: matching on amount and
        // date alone produces confident wrong answers, and a reconciliation that
        // is quietly wrong is worse than one that is visibly incomplete.
        var endToEndIds = parsed.Entries
            .Select(e => e.EndToEndId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct()
            .ToList();

        var byEndToEndId = await ResolvePaymentsAsync(companyCode, endToEndIds, ct);

        var lineNumber = 0;
        foreach (var entry in parsed.Entries)
        {
            (short FiscalYear, long DocumentNumber)? matched =
                entry.EndToEndId is { Length: > 0 } id
                && byEndToEndId.TryGetValue(id, out var found)
                    ? found
                    : null;

            statement.Lines.Add(new BankStatementLine
            {
                TenantId = tenant.TenantId,
                LineNumber = ++lineNumber,
                EntryReference = entry.EntryReference,
                Amount = Math.Abs(entry.Amount),
                IsCredit = entry.IsCredit,
                BookingDate = entry.BookingDate,
                ValueDate = entry.ValueDate,
                EndToEndId = entry.EndToEndId,
                RemittanceInformation = Truncate(entry.RemittanceInformation, 400),
                CounterpartyName = Truncate(entry.CounterpartyName, 140),
                BankTransactionCode = entry.BankTransactionCode,
                Status = matched is null
                    ? StatementLineStatus.Unmatched
                    : StatementLineStatus.Matched,
                FiscalYear = matched?.FiscalYear,
                DocumentNumber = matched?.DocumentNumber,
                MatchMethod = matched is null ? null : "EndToEndId",
                MatchedBy = matched is null ? null : "SYSTEM",
                MatchedAtUtc = matched is null ? null : now,
                CreatedBy = user.UserName,
            });
        }

        db.Add(statement);

        var unmatched = statement.Lines.Count(l => l.Status == StatementLineStatus.Unmatched);

        db.Add(new Audit.Domain.AuditLog
        {
            TenantId = tenant.TenantId,
            OccurredAtUtc = now,
            UserName = user.UserName,
            CompanyCodeId = companyCode.Id,
            Action = Audit.Domain.AuditAction.Create,
            ObjectType = "BankStatement",
            ObjectId = parsed.StatementId,
            SourceApi = "POST /api/v1/finance/bank-statements",
            TransactionCode = "FF_5",
            CorrelationId = correlation.CorrelationId,
            Summary = $"Imported {parsed.Format} for {account.HouseBank.Code}/{account.Code}: " +
                      $"{statement.Lines.Count} entries, {unmatched} unmatched, " +
                      $"closing {parsed.ClosingBalance:N2}",
        });

        return Project(statement, companyCode.Code,
            $"{account.HouseBank.Code}/{account.Code}",
            await CurrencyCodeAsync(account.CurrencyId, ct),
            alreadyImported: false);
    }

    private async Task<HouseBankAccount> ResolveAccountAsync(
        BankStatementReader.ParsedStatement parsed, CancellationToken ct)
    {
        var candidates = await db.Set<HouseBankAccount>()
            .Include(a => a.HouseBank)
            .Where(a => a.IsActive)
            .ToListAsync(ct);

        var account = candidates.FirstOrDefault(a =>
            (parsed.Iban is { Length: > 0 } && string.Equals(a.Iban, parsed.Iban, StringComparison.OrdinalIgnoreCase))
            || (parsed.AccountOther is { Length: > 0 }
                && string.Equals(a.AccountNumber, parsed.AccountOther, StringComparison.OrdinalIgnoreCase)));

        return account ?? throw new BusinessRuleException(
            BankStatementErrors.UnknownAccount,
            $"The statement is for account {parsed.Iban ?? parsed.AccountOther ?? "(unstated)"}, " +
            "which is not a house bank account in this system. Nothing has been imported.");
    }

    /// <summary>
    /// End-to-end id back to the payment document that produced it. Resolved
    /// through the payment runs of this company code rather than by taking the
    /// identifier apart, so that a change to the format breaks here loudly rather
    /// than silently matching nothing.
    /// </summary>
    private async Task<Dictionary<string, (short FiscalYear, long DocumentNumber)>>
        ResolvePaymentsAsync(CompanyCode companyCode, List<string> endToEndIds, CancellationToken ct)
    {
        if (endToEndIds.Count == 0)
        {
            return new Dictionary<string, (short, long)>(StringComparer.OrdinalIgnoreCase);
        }

        var items = await db.Set<PaymentRunItem>()
            .AsNoTracking()
            .Where(i => i.PaymentRun.CompanyCodeId == companyCode.Id
                        && !i.IsExcluded
                        && i.PaymentDocumentNumber != null)
            .Select(i => new
            {
                i.PaymentRun.RunDate,
                i.PaymentDocumentNumber,
                i.FiscalYear,
            })
            .Distinct()
            .ToListAsync(ct);

        var map = new Dictionary<string, (short, long)>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            var key = $"{companyCode.Code}-{item.RunDate.Year}-{item.PaymentDocumentNumber}";
            map.TryAdd(key, (item.FiscalYear, item.PaymentDocumentNumber!.Value));
        }

        return map;
    }

    private async Task<string> CurrencyCodeAsync(long currencyId, CancellationToken ct) =>
        await db.Set<Currency>().AsNoTracking()
            .Where(c => c.Id == currencyId).Select(c => c.Code).SingleAsync(ct);

    private static string? Truncate(string? value, int max) =>
        value is { Length: > 0 } && value.Length > max ? value[..max] : value;

    private static string Sha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    internal static BankStatementResult Project(
        BankStatement statement, string companyCode, string account, string currency,
        bool alreadyImported) =>
        new()
        {
            StatementId = statement.StatementId,
            CompanyCode = companyCode,
            HouseBankAccount = account,
            Format = statement.Format,
            StatementDate = statement.StatementDate,
            OpeningBalance = statement.OpeningBalance,
            ClosingBalance = statement.ClosingBalance,
            Currency = currency,
            ImportedAtUtc = statement.ImportedAtUtc,
            ImportedBy = statement.ImportedBy,
            LineCount = statement.Lines.Count,
            Matched = statement.Lines.Count(l => l.Status == StatementLineStatus.Matched),
            Unmatched = statement.Lines.Count(l => l.Status == StatementLineStatus.Unmatched),
            Ignored = statement.Lines.Count(l => l.Status == StatementLineStatus.Ignored),
            AlreadyImported = alreadyImported,
            Lines = statement.Lines.OrderBy(l => l.LineNumber).Select(ToView).ToList(),
        };

    internal static BankStatementLineView ToView(BankStatementLine l) => new(
        l.LineNumber, l.EntryReference, l.Amount, l.IsCredit, l.BookingDate, l.ValueDate,
        l.EndToEndId, l.RemittanceInformation, l.CounterpartyName, l.Status.ToString(),
        l.FiscalYear, l.DocumentNumber, l.MatchMethod, l.MatchedBy, l.MatchComment);
}

/// <summary>
/// Records that a statement line and a posted document are the same movement.
/// Writes only the correspondence: the statement is evidence and the document is
/// posted, and reconciliation adjusts neither.
/// </summary>
public sealed class MatchStatementLineHandler(
    S4herpDbContext db,
    IAuthorizationEnforcer authorization,
    IUserContext user,
    ITenantContext tenant,
    IClock clock,
    ICorrelationContext correlation)
    : ICommandHandler<MatchStatementLineCommand, BankStatementLineView>
{
    public async Task<BankStatementLineView> HandleAsync(
        MatchStatementLineCommand command, CancellationToken ct)
    {
        var (statement, line, companyCode) = await BankStatementLookup.LoadLineAsync(
            db, command.StatementId, command.LineNumber, ct);

        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", companyCode.Code), ("ACTVT", "01")], ct);

        if (line.Status == StatementLineStatus.Matched)
        {
            throw new BusinessRuleException(
                BankStatementErrors.AlreadyMatched,
                $"Line {command.LineNumber} of statement {command.StatementId} is already " +
                $"matched to document {line.DocumentNumber}.");
        }

        // Looked up without the status filter, then judged. Filtering on Posted
        // and reporting "no such document" tells somebody staring at a reversed
        // document that it does not exist, which sends them looking for a typo
        // that is not there.
        var header = await db.Set<JournalEntryHeader>()
            .AsNoTracking()
            .SingleOrDefaultAsync(h => h.CompanyCodeId == companyCode.Id
                                       && h.FiscalYear == command.FiscalYear
                                       && h.DocumentNumber == command.DocumentNumber, ct)
            ?? throw new BusinessRuleException(
                BankStatementErrors.DocumentNotFound,
                $"No document {command.FiscalYear}/{command.DocumentNumber} exists in " +
                $"company code {companyCode.Code}.");

        if (header.Status != JournalStatus.Posted)
        {
            throw new BusinessRuleException(
                BankStatementErrors.DocumentNotFound,
                $"Document {command.FiscalYear}/{command.DocumentNumber} is {header.Status}, " +
                "so it is not a movement on the account. A reversed payment leaves the bank " +
                "entry to be matched to something else — often the return the bank sent back.");
        }

        // The document must actually touch this bank account, and for this
        // amount. A reconciliation that lets any document be pinned to any line
        // records an opinion rather than a fact — and it is the fact that a
        // month-end close rests on.
        var bankMovement = await db.Set<JournalEntryLine>()
            .AsNoTracking()
            .Where(l => l.CompanyCodeId == companyCode.Id
                        && l.FiscalYear == command.FiscalYear
                        && l.DocumentNumber == command.DocumentNumber
                        && l.GLAccountId == statement.HouseBankAccount.GLAccountId)
            .SumAsync(l => (decimal?)l.LocalAmount, ct) ?? 0m;

        if (bankMovement == 0m)
        {
            // Two different situations reach here and a person needs to know
            // which. A document that never touched this account is a wrong
            // choice; one that touched it and was reversed nets to zero and is
            // no longer a movement anything can be matched to. Saying "does not
            // post to this account" for the second is untrue and sends the
            // reader looking in the wrong place.
            var touchesAccount = await db.Set<JournalEntryLine>()
                .AsNoTracking()
                .AnyAsync(l => l.CompanyCodeId == companyCode.Id
                               && l.FiscalYear == command.FiscalYear
                               && l.DocumentNumber == command.DocumentNumber
                               && l.GLAccountId == statement.HouseBankAccount.GLAccountId, ct);

            throw new BusinessRuleException(
                BankStatementErrors.DocumentNotFound,
                touchesAccount
                    ? $"Document {command.FiscalYear}/{command.DocumentNumber} nets to zero on " +
                      "this bank account — it has been reversed — so there is no movement to match."
                    : $"Document {command.FiscalYear}/{command.DocumentNumber} does not post to " +
                      "the bank account this statement is for.");
        }

        var expected = line.IsCredit ? line.Amount : -line.Amount;
        if (Math.Abs(bankMovement - expected) > 0.005m)
        {
            throw new BusinessRuleException(
                BankStatementErrors.AmountMismatch,
                $"The statement line moves {expected:N2} on the bank account and document " +
                $"{command.FiscalYear}/{command.DocumentNumber} moves {bankMovement:N2}. " +
                "They are not the same movement.");
        }

        var now = clock.UtcNow;
        line.Status = StatementLineStatus.Matched;
        line.FiscalYear = command.FiscalYear;
        line.DocumentNumber = command.DocumentNumber;
        line.MatchMethod = "Manual";
        line.MatchedBy = user.UserName;
        line.MatchedAtUtc = now;
        line.MatchComment = command.Comment;
        line.ModifiedBy = user.UserName;
        line.ModifiedAtUtc = now;

        db.Add(new Audit.Domain.AuditLog
        {
            TenantId = tenant.TenantId,
            OccurredAtUtc = now,
            UserName = user.UserName,
            CompanyCodeId = companyCode.Id,
            Action = Audit.Domain.AuditAction.Change,
            ObjectType = "BankStatementLine",
            ObjectId = $"{command.StatementId}/{command.LineNumber}",
            SourceApi = "POST /api/v1/finance/bank-statements/{id}/lines/{line}/match",
            TransactionCode = "FF_5",
            CorrelationId = correlation.CorrelationId,
            Summary = $"Matched to {command.FiscalYear}/{command.DocumentNumber} " +
                      $"({expected:N2}). {command.Comment}".Trim(),
        });

        return ImportBankStatementHandler.ToView(line);
    }
}

public sealed class IgnoreStatementLineHandler(
    S4herpDbContext db,
    IAuthorizationEnforcer authorization,
    IUserContext user,
    ITenantContext tenant,
    IClock clock,
    ICorrelationContext correlation)
    : ICommandHandler<IgnoreStatementLineCommand, BankStatementLineView>
{
    public async Task<BankStatementLineView> HandleAsync(
        IgnoreStatementLineCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            throw new RequestValidationException(
                [new RuleViolation("reason", "REQUIRED",
                    "Say why this line needs no document. An unexplained dismissal is " +
                    "indistinguishable from an oversight.")]);
        }

        var (_, line, companyCode) = await BankStatementLookup.LoadLineAsync(
            db, command.StatementId, command.LineNumber, ct);

        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", companyCode.Code), ("ACTVT", "01")], ct);

        if (line.Status == StatementLineStatus.Matched)
        {
            throw new BusinessRuleException(
                BankStatementErrors.AlreadyMatched,
                $"Line {command.LineNumber} is matched to document {line.DocumentNumber}. " +
                "Nothing that corresponds to a posted document should be set aside.");
        }

        var now = clock.UtcNow;
        line.Status = StatementLineStatus.Ignored;
        line.MatchMethod = "Ignored";
        line.MatchedBy = user.UserName;
        line.MatchedAtUtc = now;
        line.MatchComment = command.Reason;
        line.ModifiedBy = user.UserName;
        line.ModifiedAtUtc = now;

        db.Add(new Audit.Domain.AuditLog
        {
            TenantId = tenant.TenantId,
            OccurredAtUtc = now,
            UserName = user.UserName,
            CompanyCodeId = companyCode.Id,
            Action = Audit.Domain.AuditAction.Change,
            ObjectType = "BankStatementLine",
            ObjectId = $"{command.StatementId}/{command.LineNumber}",
            SourceApi = "POST /api/v1/finance/bank-statements/{id}/lines/{line}/ignore",
            TransactionCode = "FF_5",
            CorrelationId = correlation.CorrelationId,
            Summary = $"Set aside as needing no document: {command.Reason}",
        });

        return ImportBankStatementHandler.ToView(line);
    }
}

internal static class BankStatementLookup
{
    public static async Task<(BankStatement Statement, BankStatementLine Line, CompanyCode CompanyCode)>
        LoadLineAsync(S4herpDbContext db, string statementId, int lineNumber, CancellationToken ct)
    {
        var statement = await db.Set<BankStatement>()
            .Include(s => s.Lines)
            .Include(s => s.HouseBankAccount).ThenInclude(a => a.HouseBank)
            .Include(s => s.CompanyCode)
            .SingleOrDefaultAsync(s => s.StatementId == statementId, ct)
            ?? throw new NotFoundException($"Bank statement {statementId} has not been imported.");

        var line = statement.Lines.SingleOrDefault(l => l.LineNumber == lineNumber)
            ?? throw new BusinessRuleException(
                BankStatementErrors.LineNotFound,
                $"Statement {statementId} has no line {lineNumber}.");

        return (statement, line, statement.CompanyCode);
    }
}

public sealed class GetBankStatementQueryHandler(
    S4herpDbContext db, IAuthorizationEnforcer authorization)
    : IQueryHandler<GetBankStatementQuery, BankStatementResult>
{
    public async Task<BankStatementResult> HandleAsync(
        GetBankStatementQuery query, CancellationToken ct)
    {
        var statement = await db.Set<BankStatement>()
            .AsNoTracking()
            .Include(s => s.Lines)
            .Include(s => s.HouseBankAccount).ThenInclude(a => a.HouseBank)
            .Include(s => s.CompanyCode)
            .SingleOrDefaultAsync(s => s.StatementId == query.StatementId, ct)
            ?? throw new NotFoundException(
                $"Bank statement {query.StatementId} has not been imported.");

        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", statement.CompanyCode.Code), ("ACTVT", "03")], ct);

        var currency = await db.Set<Currency>().AsNoTracking()
            .Where(c => c.Id == statement.CurrencyId).Select(c => c.Code).SingleAsync(ct);

        return ImportBankStatementHandler.Project(
            statement, statement.CompanyCode.Code,
            $"{statement.HouseBankAccount.HouseBank.Code}/{statement.HouseBankAccount.Code}",
            currency, alreadyImported: true);
    }
}

public sealed class ListBankStatementsQueryHandler(
    S4herpDbContext db, IAuthorizationEnforcer authorization)
    : IQueryHandler<ListBankStatementsQuery, IReadOnlyList<BankStatementSummary>>
{
    public async Task<IReadOnlyList<BankStatementSummary>> HandleAsync(
        ListBankStatementsQuery query, CancellationToken ct)
    {
        var companyCodes = await authorization.AuthorizedCompanyCodeIdsAsync(ct);

        var statements = db.Set<BankStatement>()
            .AsNoTracking()
            .Where(s => companyCodes.Contains(s.CompanyCodeId));

        if (query.CompanyCode is { Length: > 0 } code)
        {
            statements = statements.Where(s => s.CompanyCode.Code == code);
        }

        var rows = await statements
            .OrderByDescending(s => s.StatementDate).ThenByDescending(s => s.StatementId)
            .Take(Math.Clamp(query.Take, 1, 200))
            .Select(s => new
            {
                s.StatementId,
                CompanyCode = s.CompanyCode.Code,
                Account = s.HouseBankAccount.HouseBank.Code + "/" + s.HouseBankAccount.Code,
                s.StatementDate,
                s.ClosingBalance,
                s.CurrencyId,
                LineCount = s.Lines.Count,
                Unmatched = s.Lines.Count(l => l.Status == StatementLineStatus.Unmatched),
            })
            .ToListAsync(ct);

        var currencyIds = rows.Select(r => r.CurrencyId).Distinct().ToList();
        var currencies = await db.Set<Currency>().AsNoTracking()
            .Where(c => currencyIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Code, ct);

        return rows
            .Select(r => new BankStatementSummary(
                r.StatementId, r.CompanyCode, r.Account, r.StatementDate, r.ClosingBalance,
                currencies.GetValueOrDefault(r.CurrencyId, "?"), r.LineCount, r.Unmatched))
            .ToList();
    }
}

/// <summary>
/// The question a month-end close is blocked on: does the bank agree with the
/// ledger, and if not, by how much and on account of what.
/// </summary>
public sealed class GetBankReconciliationQueryHandler(
    S4herpDbContext db, IAuthorizationEnforcer authorization)
    : IQueryHandler<GetBankReconciliationQuery, BankReconciliationResult>
{
    public async Task<BankReconciliationResult> HandleAsync(
        GetBankReconciliationQuery query, CancellationToken ct)
    {
        var statement = await db.Set<BankStatement>()
            .AsNoTracking()
            .Include(s => s.Lines)
            .Include(s => s.HouseBankAccount).ThenInclude(a => a.HouseBank)
            .Include(s => s.HouseBankAccount).ThenInclude(a => a.GLAccount)
            .Include(s => s.CompanyCode)
            .SingleOrDefaultAsync(s => s.StatementId == query.StatementId, ct)
            ?? throw new NotFoundException(
                $"Bank statement {query.StatementId} has not been imported.");

        await authorization.RequireAsync(
            "F_BKPF_BUK", [("BUKRS", statement.CompanyCode.Code), ("ACTVT", "03")], ct);

        var currency = await db.Set<Currency>().AsNoTracking()
            .Where(c => c.Id == statement.CurrencyId).Select(c => c.Code).SingleAsync(ct);

        // Everything posted to the bank G/L account on or before the statement
        // date. Derived from the journal for the same reason the trial balance is:
        // a maintained balance is a second copy of the truth, and the two drift.
        var ledgerBalance = await (
            from line in db.Set<JournalEntryLine>().AsNoTracking()
            join header in db.Set<JournalEntryHeader>()
                on new { line.TenantId, line.CompanyCodeId, line.FiscalYear, line.DocumentNumber }
                equals new { header.TenantId, header.CompanyCodeId, header.FiscalYear, header.DocumentNumber }
            where line.CompanyCodeId == statement.CompanyCodeId
                  && line.GLAccountId == statement.HouseBankAccount.GLAccountId
                  && header.Status == JournalStatus.Posted
                  && header.PostingDate <= statement.StatementDate
            select (decimal?)line.LocalAmount).SumAsync(ct) ?? 0m;

        var unmatched = statement.Lines
            .Where(l => l.Status == StatementLineStatus.Unmatched)
            .OrderBy(l => l.LineNumber)
            .ToList();

        var difference = statement.ClosingBalance - ledgerBalance;

        return new BankReconciliationResult
        {
            StatementId = statement.StatementId,
            CompanyCode = statement.CompanyCode.Code,
            HouseBankAccount =
                $"{statement.HouseBankAccount.HouseBank.Code}/{statement.HouseBankAccount.Code}",
            GLAccount = statement.HouseBankAccount.GLAccount.AccountNumber,
            StatementDate = statement.StatementDate,
            Currency = currency,
            StatementClosingBalance = statement.ClosingBalance,
            LedgerBalance = ledgerBalance,
            Difference = difference,
            // Half a cent, because decimal(19,4) and a bank's two decimals do not
            // always agree on the last digit. Anything larger is a real gap.
            IsReconciled = Math.Abs(difference) < 0.005m,
            UnmatchedLines = unmatched.Select(ImportBankStatementHandler.ToView).ToList(),
            UnmatchedTotal = unmatched.Sum(l => l.IsCredit ? l.Amount : -l.Amount),
        };
    }
}
