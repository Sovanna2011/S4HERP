using Microsoft.EntityFrameworkCore;
using S4HERP.BuildingBlocks.Application;
using S4HERP.BuildingBlocks.Infrastructure;
using S4HERP.Controlling.Domain;
using S4HERP.Finance.Domain;
using S4HERP.Organization.Domain;

namespace S4HERP.Finance.Application;

/// <summary>
/// The central posting engine (blueprint §5.4). Every financial posting enters
/// here, and this is the only code that writes <c>fin.JournalEntryLine</c>.
///
/// The order of steps is the design: authorise before validating so a caller
/// cannot probe rules on data they may not see; derive the period rather than
/// trusting the caller; allocate the document number last so the number-range
/// row lock is held for as short a time as possible (ADR-11).
/// </summary>
public sealed class PostJournalEntryHandler(
    S4herpDbContext db,
    IFiscalPeriodService periods,
    ICurrencyTranslator currencies,
    INumberRangeAllocator numbers,
    IAuthorizationEnforcer authorization,
    IUserContext user,
    ITenantContext tenant,
    IClock clock,
    ICorrelationContext correlation)
    : ICommandHandler<PostJournalEntryCommand, PostJournalEntryResult>
{
    public async Task<PostJournalEntryResult> HandleAsync(
        PostJournalEntryCommand command, CancellationToken cancellationToken)
    {
        // 1 — idempotency. Before anything else, so a replay costs one lookup.
        if (!command.Simulate && command.IdempotencyKey is { Length: > 0 } key)
        {
            var replay = await FindReplayAsync(key, cancellationToken);
            if (replay is not null)
            {
                return replay;
            }
        }

        // 2 — structural resolution.
        var context = await ResolveContextAsync(command, cancellationToken);

        // 3 — fine-grained authorisation, now that the company code is known.
        await authorization.RequireAsync(
            "F_BKPF_BUK",
            [("BUKRS", command.CompanyCode), ("ACTVT", "01")],
            cancellationToken);
        await authorization.RequireAsync(
            "F_BKPF_BLA",
            [("BLART", command.DocumentType), ("ACTVT", "01")],
            cancellationToken);

        // 4 — derive the period, then check it is open for the account types present.
        var period = await periods.DeriveAsync(
            context.CompanyCode.Id, command.PostingDate, cancellationToken);

        var lines = await DeriveLinesAsync(command, context, cancellationToken);

        // Tax before balancing: the caller enters the base lines, the engine
        // completes the document. Balancing a document that is still missing its
        // tax lines would reject every correct invoice.
        await GenerateTaxLinesAsync(lines, context, command.PostingDate, cancellationToken);

        await periods.RequireOpenAsync(
            context.CompanyCode.Id, period,
            lines.Select(l => l.AccountType).Distinct().ToList(),
            cancellationToken);

        // 5 — balance, in every currency.
        var totals = CheckBalanced(lines, context);

        var result = new PostJournalEntryResult
        {
            Posted = false,
            CompanyCode = command.CompanyCode,
            FiscalYear = period.FiscalYear,
            FiscalPeriod = period.Period,
            Lines = lines.Select(l => l.ToDto(context)).ToList(),
            Totals = totals,
        };

        if (command.Simulate)
        {
            return result;
        }

        // 6 — allocate and write. Nothing that can fail runs after allocation.
        var documentNumber = await numbers.AllocateAsync(
            NumberRangeObject.AccountingDocument,
            context.DocumentType.NumberRangeCode,
            context.CompanyCode.Id,
            period.FiscalYear,
            cancellationToken);

        var formatted =
            $"KSS-{context.CompanyCode.Code}-{period.FiscalYear}-{command.DocumentType}-{documentNumber:D10}";

        var status = command.Park ? JournalStatus.Parked : JournalStatus.Posted;
        var header = WriteDocument(command, context, period, lines, documentNumber, formatted, status);

        // Open items are a ledger fact. A parked document has not reached the
        // ledger, so it owes nobody anything yet; approval creates them.
        if (!command.Park)
        {
            await WriteOpenItemsAsync(header, lines, context, cancellationToken);
        }

        if (command.IdempotencyKey is { Length: > 0 } idempotencyKey)
        {
            db.Add(new PostingIdempotency
            {
                TenantId = tenant.TenantId,
                IdempotencyKey = idempotencyKey,
                CompanyCodeId = context.CompanyCode.Id,
                FiscalYear = period.FiscalYear,
                DocumentNumber = documentNumber,
                CreatedAtUtc = clock.UtcNow,
            });
        }

        WriteAudit(context, period, documentNumber, formatted, command.Park);

        return result with
        {
            Posted = !command.Park,
            Status = status.ToString(),
            DocumentNumber = documentNumber,
            DocumentNumberFormatted = formatted,
        };
    }

    // ---------------------------------------------------------------- context

    private sealed record PostingContext
    {
        public required CompanyCode CompanyCode { get; init; }
        public required DocumentType DocumentType { get; init; }
        public required Ledger Ledger { get; init; }
        public required Currency DocumentCurrency { get; init; }
        public required Currency LocalCurrency { get; init; }
        public required long ExchangeRateTypeId { get; init; }
        public required decimal RateToLocal { get; init; }
        public required Dictionary<string, GLAccount> Accounts { get; init; }
        public required Dictionary<string, GLAccountCompanyCode> AccountSettings { get; init; }
        public required Dictionary<string, PostingKey> PostingKeys { get; init; }
        public required Dictionary<string, long> Partners { get; init; }

        /// <summary>
        /// Partner number to its reconciliation account in this company code. A
        /// subledger line carries that account so the G/L view is complete from
        /// the journal alone, without joining the partner master.
        /// </summary>
        public required Dictionary<string, long> ReconciliationAccounts { get; init; }

        /// <summary>Partner number to the payment terms on its company-code facet.</summary>
        public required Dictionary<string, string?> PartnerPaymentTerms { get; init; }

        /// <summary>Payment terms by code, for due-date derivation.</summary>
        public required Dictionary<string, PaymentTerm> PaymentTerms { get; init; }

        /// <summary>The three dates a baseline rule may pick from.</summary>
        public required DateOnly DocumentDate { get; init; }
        public required DateOnly PostingDate { get; init; }
        public required DateOnly EntryDate { get; init; }
        public required Dictionary<string, CostCenter> CostCenters { get; init; }
        public required Dictionary<string, ProfitCenter> ProfitCenters { get; init; }
        public required Dictionary<long, string> ProfitCenterCodes { get; init; }
        public required Dictionary<string, long> Segments { get; init; }
        public required Dictionary<long, string> SegmentCodes { get; init; }
        public required Dictionary<string, long> CompanyCodes { get; init; }
        public required Dictionary<long, string> CompanyCodeCodes { get; init; }
    }

    private async Task<PostingContext> ResolveContextAsync(
        PostJournalEntryCommand command, CancellationToken ct)
    {
        var companyCode = await db.Set<CompanyCode>()
            .SingleOrDefaultAsync(c => c.Code == command.CompanyCode, ct)
            ?? throw new BusinessRuleException(
                PostingErrors.UnknownObject, $"Company code {command.CompanyCode} does not exist.");

        if (!companyCode.IsActive)
        {
            throw new BusinessRuleException(
                PostingErrors.AccountBlocked, $"Company code {command.CompanyCode} is not active.");
        }

        var documentType = await db.Set<DocumentType>()
            .SingleOrDefaultAsync(d => d.Code == command.DocumentType, ct)
            ?? throw new BusinessRuleException(
                PostingErrors.UnknownObject, $"Document type {command.DocumentType} does not exist.");

        var ledger = await db.Set<Ledger>()
            .SingleOrDefaultAsync(l => l.LedgerType == LedgerType.Leading && l.IsActive, ct)
            ?? throw new BusinessRuleException(
                PostingErrors.UnknownObject, "No leading ledger is configured.");

        var documentCurrency = await db.Set<Currency>()
            .SingleOrDefaultAsync(c => c.Code == command.Currency, ct)
            ?? throw new BusinessRuleException(
                PostingErrors.UnknownObject, $"Currency {command.Currency} does not exist.");

        var localCurrency = await db.Set<Currency>()
            .SingleAsync(c => c.Id == companyCode.LocalCurrencyId, ct);

        var rateType = await db.Set<ExchangeRateType>().FirstAsync(t => t.Code == "M", ct);

        var rateToLocal = command.ExchangeRate
            ?? await currencies.RateAsync(
                documentCurrency.Id, localCurrency.Id, command.PostingDate, "M", ct);

        var accountNumbers = command.Lines
            .Select(l => l.GLAccount).Where(a => a is not null).Distinct().ToList();
        var accounts = await db.Set<GLAccount>()
            .Where(a => a.ChartOfAccountsId == companyCode.ChartOfAccountsId
                        && accountNumbers.Contains(a.AccountNumber))
            .ToDictionaryAsync(a => a.AccountNumber, ct);

        var accountIds = accounts.Values.Select(a => a.Id).ToList();
        var settings = await db.Set<GLAccountCompanyCode>()
            .Where(s => s.CompanyCodeId == companyCode.Id && accountIds.Contains(s.GLAccountId))
            .ToListAsync(ct);
        var settingsByNumber = settings.ToDictionary(
            s => accounts.First(a => a.Value.Id == s.GLAccountId).Key);

        var postingKeyCodes = command.Lines.Select(l => l.PostingKey).Distinct().ToList();
        var postingKeys = await db.Set<PostingKey>()
            .Where(p => postingKeyCodes.Contains(p.Code))
            .ToDictionaryAsync(p => p.Code, ct);

        var partnerNumbers = command.Lines
            .Select(l => l.BusinessPartner).Where(p => p is not null).Distinct().ToList();
        var partners = await db.Set<BusinessPartner.Domain.Partner>()
            .Where(p => partnerNumbers.Contains(p.PartnerNumber))
            .ToDictionaryAsync(p => p.PartnerNumber, p => p.Id, ct);

        var partnerIds = partners.Values.ToList();
        var reconciliationAccounts = await (
            from facet in db.Set<BusinessPartner.Domain.PartnerCompanyCode>()
            join partner in db.Set<BusinessPartner.Domain.Partner>()
                on facet.PartnerId equals partner.Id
            where facet.CompanyCodeId == companyCode.Id && partnerIds.Contains(facet.PartnerId)
            select new { partner.PartnerNumber, facet.ReconciliationAccountId })
            .ToDictionaryAsync(x => x.PartnerNumber, x => x.ReconciliationAccountId, ct);

        var partnerTerms = await (
            from facet in db.Set<BusinessPartner.Domain.PartnerCompanyCode>()
            join partner in db.Set<BusinessPartner.Domain.Partner>()
                on facet.PartnerId equals partner.Id
            where facet.CompanyCodeId == companyCode.Id && partnerIds.Contains(facet.PartnerId)
            select new { partner.PartnerNumber, facet.PaymentTerms })
            .ToDictionaryAsync(x => x.PartnerNumber, x => (string?)x.PaymentTerms, ct);

        var today = DateOnly.FromDateTime(clock.UtcNow);
        var paymentTerms = await db.Set<PaymentTerm>()
            .Where(t => t.IsActive)
            .ToDictionaryAsync(t => t.Code, ct);

        var costCenters = await db.Set<CostCenter>()
            .Where(c => c.CompanyCodeId == companyCode.Id)
            .ToDictionaryAsync(c => c.Code, ct);
        var profitCenters = await db.Set<ProfitCenter>().ToDictionaryAsync(p => p.Code, ct);
        var segments = await db.Set<Segment>().ToListAsync(ct);
        var companyCodes = await db.Set<CompanyCode>().ToListAsync(ct);

        return new PostingContext
        {
            CompanyCode = companyCode,
            DocumentType = documentType,
            Ledger = ledger,
            DocumentCurrency = documentCurrency,
            LocalCurrency = localCurrency,
            ExchangeRateTypeId = rateType.Id,
            RateToLocal = rateToLocal,
            Accounts = accounts,
            AccountSettings = settingsByNumber,
            PostingKeys = postingKeys,
            Partners = partners,
            ReconciliationAccounts = reconciliationAccounts,
            PartnerPaymentTerms = partnerTerms,
            PaymentTerms = paymentTerms,
            DocumentDate = command.DocumentDate,
            PostingDate = command.PostingDate,
            EntryDate = today,
            CostCenters = costCenters,
            ProfitCenters = profitCenters,
            ProfitCenterCodes = profitCenters.ToDictionary(p => p.Value.Id, p => p.Key),
            Segments = segments.ToDictionary(s => s.Code, s => s.Id),
            SegmentCodes = segments.ToDictionary(s => s.Id, s => s.Code),
            CompanyCodes = companyCodes.ToDictionary(c => c.Code, c => c.Id),
            CompanyCodeCodes = companyCodes.ToDictionary(c => c.Id, c => c.Code),
        };
    }

    // ---------------------------------------------------------------- lines

    private sealed class WorkingLine
    {
        public short LineNumber { get; init; }
        public required string PostingKey { get; init; }
        public DebitCredit DebitCredit { get; init; }
        public AccountType AccountType { get; init; }
        public long? GLAccountId { get; init; }
        public string? GLAccountNumber { get; init; }
        public long? BusinessPartnerId { get; init; }
        public string? BusinessPartnerNumber { get; init; }
        public string? BusinessPartnerRole { get; init; }
        public long? CostCenterId { get; set; }
        public long? ProfitCenterId { get; set; }
        public long? SegmentId { get; set; }
        public long? PartnerCompanyCodeId { get; init; }
        public decimal DocumentAmount { get; init; }
        public decimal LocalAmount { get; init; }
        public string? TaxCode { get; init; }
        public decimal? TaxBaseAmount { get; init; }
        public decimal? TaxAmount { get; init; }
        public bool IsTaxLine { get; init; }
        public bool IsGenerated { get; init; }
        public string? Assignment { get; init; }
        public string? LineText { get; init; }
        public DateOnly? DueDate { get; init; }
        public string? PaymentTerms { get; init; }
        public bool IsOpenItemManaged { get; init; }

        public DerivedLine ToDto(PostingContext context) => new()
        {
            LineNumber = LineNumber,
            PostingKey = PostingKey,
            DebitCredit = ((char)DebitCredit).ToString(),
            AccountType = ((char)AccountType).ToString(),
            GLAccount = GLAccountNumber,
            BusinessPartner = BusinessPartnerNumber,
            CostCenter = CostCenterId is null
                ? null
                : context.CostCenters.FirstOrDefault(c => c.Value.Id == CostCenterId).Key,
            ProfitCenter = ProfitCenterId is null
                ? null
                : context.ProfitCenterCodes.GetValueOrDefault(ProfitCenterId.Value),
            Segment = SegmentId is null
                ? null
                : context.SegmentCodes.GetValueOrDefault(SegmentId.Value),
            PartnerCompanyCode = PartnerCompanyCodeId is null
                ? null
                : context.CompanyCodeCodes.GetValueOrDefault(PartnerCompanyCodeId.Value),
            DocumentAmount = DocumentAmount,
            DocumentCurrency = context.DocumentCurrency.Code,
            LocalAmount = LocalAmount,
            LocalCurrency = context.LocalCurrency.Code,
            TaxCode = TaxCode,
            IsGenerated = IsGenerated,
            LineText = LineText,
        };
    }

    private async Task<List<WorkingLine>> DeriveLinesAsync(
        PostJournalEntryCommand command, PostingContext context, CancellationToken ct)
    {
        var violations = new List<RuleViolation>();
        var lines = new List<WorkingLine>();
        short lineNumber = 0;

        foreach (var input in command.Lines)
        {
            lineNumber++;
            var field = $"lines[{lineNumber - 1}]";

            if (!context.PostingKeys.TryGetValue(input.PostingKey, out var postingKey))
            {
                violations.Add(new RuleViolation($"{field}.postingKey",
                    PostingErrors.UnknownObject, $"Posting key {input.PostingKey} does not exist."));
                continue;
            }

            if (!context.DocumentType.AllowedAccountTypes.Contains((char)postingKey.AccountType))
            {
                violations.Add(new RuleViolation($"{field}.postingKey",
                    PostingErrors.DocumentTypeNotAllowed,
                    $"Document type {context.DocumentType.Code} does not permit account type " +
                    $"{(char)postingKey.AccountType}."));
                continue;
            }

            // Debits positive, credits negative, so a balanced document sums to zero.
            var signed = postingKey.IsDebit ? Math.Abs(input.Amount) : -Math.Abs(input.Amount);

            var line = postingKey.AccountType switch
            {
                AccountType.GeneralLedger =>
                    BuildGeneralLedgerLine(input, postingKey, lineNumber, signed, field, context, violations),
                AccountType.Customer or AccountType.Vendor =>
                    BuildPartnerLine(input, postingKey, lineNumber, signed, field, context, violations),
                _ => null,
            };

            if (line is null)
            {
                continue;
            }

            DeriveControllingObjects(line, input, field, context, violations);
            lines.Add(line);
        }

        if (violations.Count > 0)
        {
            throw new BusinessRuleException(
                PostingErrors.UnknownObject, "The document could not be derived.", violations);
        }

        await Task.CompletedTask;
        return lines;
    }

    private WorkingLine? BuildGeneralLedgerLine(
        JournalLineInput input, PostingKey postingKey, short lineNumber, decimal signed,
        string field, PostingContext context, List<RuleViolation> violations)
    {
        if (input.GLAccount is null)
        {
            violations.Add(new RuleViolation($"{field}.glAccount",
                PostingErrors.UnknownObject, "A G/L account is required for account type S."));
            return null;
        }

        if (!context.Accounts.TryGetValue(input.GLAccount, out var account))
        {
            violations.Add(new RuleViolation($"{field}.glAccount",
                PostingErrors.UnknownObject,
                $"G/L account {input.GLAccount} does not exist in chart of accounts " +
                $"{context.CompanyCode.ChartOfAccountsId}."));
            return null;
        }

        // A reconciliation account carries a subledger balance; posting to it
        // directly would make the subledger and the G/L disagree permanently.
        if (account.IsReconciliationAccount)
        {
            violations.Add(new RuleViolation($"{field}.glAccount",
                PostingErrors.ReconciliationAccount,
                $"G/L account {input.GLAccount} is a reconciliation account for account type " +
                $"{(char?)account.ReconciliationAccountType}. Post through the subledger instead."));
            return null;
        }

        if (account.IsBlocked || !account.IsActive)
        {
            violations.Add(new RuleViolation($"{field}.glAccount",
                PostingErrors.AccountBlocked, $"G/L account {input.GLAccount} is blocked."));
            return null;
        }

        context.AccountSettings.TryGetValue(input.GLAccount, out var settings);
        if (settings is { IsPostingBlocked: true })
        {
            violations.Add(new RuleViolation($"{field}.glAccount",
                PostingErrors.AccountBlocked,
                $"G/L account {input.GLAccount} is blocked for posting in company code " +
                $"{context.CompanyCode.Code}."));
            return null;
        }

        if (settings is { RequiresCostObject: true }
            && input.CostCenter is null && input.InternalOrder is null)
        {
            violations.Add(new RuleViolation($"{field}.costCenter",
                PostingErrors.CostObjectRequired,
                $"G/L account {input.GLAccount} requires a cost centre or internal order."));
            return null;
        }

        return new WorkingLine
        {
            LineNumber = lineNumber,
            PostingKey = postingKey.Code,
            DebitCredit = postingKey.IsDebit ? DebitCredit.Debit : DebitCredit.Credit,
            AccountType = AccountType.GeneralLedger,
            GLAccountId = account.Id,
            GLAccountNumber = account.AccountNumber,
            PartnerCompanyCodeId = ResolvePartnerCompanyCode(input, field, context, violations),
            DocumentAmount = signed,
            LocalAmount = currencies.Translate(signed, context.RateToLocal),
            TaxCode = input.TaxCode,
            Assignment = input.Assignment,
            LineText = input.LineText,
            IsOpenItemManaged = settings?.IsOpenItemManaged ?? false,
        };
    }

    private WorkingLine? BuildPartnerLine(
        JournalLineInput input, PostingKey postingKey, short lineNumber, decimal signed,
        string field, PostingContext context, List<RuleViolation> violations)
    {
        if (input.BusinessPartner is null)
        {
            violations.Add(new RuleViolation($"{field}.businessPartner",
                PostingErrors.UnknownObject,
                $"A business partner is required for account type {(char)postingKey.AccountType}."));
            return null;
        }

        if (!context.Partners.TryGetValue(input.BusinessPartner, out var partnerId))
        {
            violations.Add(new RuleViolation($"{field}.businessPartner",
                PostingErrors.UnknownObject,
                $"Business partner {input.BusinessPartner} does not exist."));
            return null;
        }

        var role = postingKey.AccountType == AccountType.Customer ? "FI_CUST" : "FI_VEND";

        if (!context.ReconciliationAccounts.TryGetValue(input.BusinessPartner, out var reconciliation))
        {
            violations.Add(new RuleViolation($"{field}.businessPartner",
                PostingErrors.UnknownObject,
                $"Business partner {input.BusinessPartner} has no company-code data in " +
                $"{context.CompanyCode.Code}, so no reconciliation account can be determined."));
            return null;
        }

        // Payment terms: explicit wins, then the partner's company-code facet.
        // Derived rather than demanded, because the terms agreed with a customer
        // live on the customer, not in whatever the caller happened to type.
        var paymentTerms = input.PaymentTerms
            ?? context.PartnerPaymentTerms.GetValueOrDefault(input.BusinessPartner);

        var dueDate = ResolveDueDate(input, paymentTerms, field, context, violations);

        return new WorkingLine
        {
            LineNumber = lineNumber,
            PostingKey = postingKey.Code,
            DebitCredit = postingKey.IsDebit ? DebitCredit.Debit : DebitCredit.Credit,
            AccountType = postingKey.AccountType,
            // The reconciliation account, so a trial balance reads the journal
            // alone. Direct posting to it is still refused — only the subledger
            // may put a value here.
            GLAccountId = reconciliation,
            BusinessPartnerId = partnerId,
            BusinessPartnerNumber = input.BusinessPartner,
            BusinessPartnerRole = role,
            PartnerCompanyCodeId = ResolvePartnerCompanyCode(input, field, context, violations),
            DocumentAmount = signed,
            LocalAmount = currencies.Translate(signed, context.RateToLocal),
            Assignment = input.Assignment,
            LineText = input.LineText,
            DueDate = dueDate,
            PaymentTerms = paymentTerms,
            IsOpenItemManaged = true,
        };
    }

    /// <summary>
    /// The due date, derived from the payment term rather than taken on trust.
    /// A caller-supplied date still wins — a negotiated one-off exists — but the
    /// default is now computed from the term the partner actually has, and an
    /// unknown term is an error rather than a silently missing due date.
    /// </summary>
    private DateOnly? ResolveDueDate(
        JournalLineInput input, string? paymentTerms, string field,
        PostingContext context, List<RuleViolation> violations)
    {
        if (input.DueDate is { } explicitDate)
        {
            return explicitDate;
        }

        if (paymentTerms is null)
        {
            // No term configured anywhere: the item is payable on the spot, which
            // is what "no terms" means, rather than never falling due at all.
            return context.PostingDate;
        }

        if (!context.PaymentTerms.TryGetValue(paymentTerms, out var term))
        {
            violations.Add(new RuleViolation($"{field}.paymentTerms",
                PostingErrors.UnknownObject,
                $"Payment terms {paymentTerms} do not exist or are not active."));
            return null;
        }

        return term.DueDate(context.DocumentDate, context.PostingDate, context.EntryDate);
    }

    private static long? ResolvePartnerCompanyCode(
        JournalLineInput input, string field, PostingContext context, List<RuleViolation> violations)
    {
        if (input.PartnerCompanyCode is null)
        {
            return null;
        }

        if (context.CompanyCodes.TryGetValue(input.PartnerCompanyCode, out var id))
        {
            return id;
        }

        violations.Add(new RuleViolation($"{field}.partnerCompanyCode",
            PostingErrors.UnknownObject,
            $"Partner company code {input.PartnerCompanyCode} does not exist."));
        return null;
    }

    /// <summary>
    /// Derivation, not validation: an explicit profit centre wins, otherwise it
    /// comes from the cost centre, and the segment comes from the profit centre.
    /// </summary>
    private static void DeriveControllingObjects(
        WorkingLine line, JournalLineInput input, string field,
        PostingContext context, List<RuleViolation> violations)
    {
        if (input.CostCenter is { } costCenterCode)
        {
            if (!context.CostCenters.TryGetValue(costCenterCode, out var costCenter))
            {
                violations.Add(new RuleViolation($"{field}.costCenter",
                    PostingErrors.UnknownObject,
                    $"Cost centre {costCenterCode} does not exist in company code " +
                    $"{context.CompanyCode.Code}."));
                return;
            }

            if (costCenter.IsLockedForActualPosting)
            {
                violations.Add(new RuleViolation($"{field}.costCenter",
                    PostingErrors.AccountBlocked,
                    $"Cost centre {costCenterCode} is locked for actual postings."));
                return;
            }

            line.CostCenterId = costCenter.Id;
            line.ProfitCenterId = costCenter.ProfitCenterId;
        }

        if (input.ProfitCenter is { } profitCenterCode)
        {
            if (!context.ProfitCenters.TryGetValue(profitCenterCode, out var profitCenter))
            {
                violations.Add(new RuleViolation($"{field}.profitCenter",
                    PostingErrors.UnknownObject,
                    $"Profit centre {profitCenterCode} does not exist."));
                return;
            }

            line.ProfitCenterId = profitCenter.Id;
        }

        if (line.ProfitCenterId is { } derivedProfitCenter)
        {
            var code = context.ProfitCenterCodes.GetValueOrDefault(derivedProfitCenter);
            if (code is not null && context.ProfitCenters.TryGetValue(code, out var pc))
            {
                line.SegmentId = pc.SegmentId;
            }
        }

        if (input.Segment is { } segmentCode)
        {
            if (!context.Segments.TryGetValue(segmentCode, out var segmentId))
            {
                violations.Add(new RuleViolation($"{field}.segment",
                    PostingErrors.UnknownObject, $"Segment {segmentCode} does not exist."));
                return;
            }

            line.SegmentId = segmentId;
        }
    }

    // ------------------------------------------------------------------ tax

    /// <summary>
    /// Generates one tax line per (tax code, direction) from the base lines that
    /// carry that code. Grouped rather than per line, because a tax authority
    /// wants one posting per rate, not one per expense line.
    /// </summary>
    private async Task GenerateTaxLinesAsync(
        List<WorkingLine> lines, PostingContext context, DateOnly postingDate, CancellationToken ct)
    {
        var taxed = lines
            .Where(l => l.TaxCode is not null && !l.IsTaxLine)
            .GroupBy(l => l.TaxCode!)
            .ToList();

        if (taxed.Count == 0)
        {
            return;
        }

        var codes = taxed.Select(g => g.Key).ToList();
        var taxCodes = await db.Set<TaxCode>()
            .Where(t => codes.Contains(t.Code)
                        && t.CountryCode == context.CompanyCode.CountryCode
                        && t.ValidFrom <= postingDate && t.ValidTo > postingDate
                        && t.IsActive)
            .ToListAsync(ct);

        var violations = new List<RuleViolation>();
        var nextLine = (short)(lines.Max(l => l.LineNumber) + 1);

        foreach (var group in taxed)
        {
            var taxCode = taxCodes.SingleOrDefault(t => t.Code == group.Key);
            if (taxCode is null)
            {
                violations.Add(new RuleViolation("lines", PostingErrors.UnknownObject,
                    $"Tax code {group.Key} is not valid for country " +
                    $"{context.CompanyCode.CountryCode} on {postingDate:yyyy-MM-dd}."));
                continue;
            }

            if (taxCode.Rate == 0)
            {
                continue;
            }

            if (taxCode.TaxAccountId is not { } taxAccountId)
            {
                violations.Add(new RuleViolation("lines", PostingErrors.UnknownObject,
                    $"Tax code {taxCode.Code} has no tax account configured."));
                continue;
            }

            // Debits and credits within one tax code are netted before the rate is
            // applied, so a credit memo line reduces the tax rather than adding to it.
            var baseAmount = group.Sum(l => l.DocumentAmount);
            var taxAmount = decimal.Round(baseAmount * taxCode.Rate / 100m, 4, MidpointRounding.AwayFromZero);
            if (taxAmount == 0)
            {
                continue;
            }

            var accountNumber = context.Accounts.Values
                .FirstOrDefault(a => a.Id == taxAccountId)?.AccountNumber;

            lines.Add(new WorkingLine
            {
                LineNumber = nextLine++,
                PostingKey = taxAmount > 0 ? "40" : "50",
                DebitCredit = taxAmount > 0 ? DebitCredit.Debit : DebitCredit.Credit,
                AccountType = AccountType.GeneralLedger,
                GLAccountId = taxAccountId,
                GLAccountNumber = accountNumber,
                DocumentAmount = taxAmount,
                LocalAmount = currencies.Translate(taxAmount, context.RateToLocal),
                TaxCode = taxCode.Code,
                TaxBaseAmount = baseAmount,
                TaxAmount = taxAmount,
                IsTaxLine = true,
                IsGenerated = true,
                LineText = $"{taxCode.Name} on {Math.Abs(baseAmount):N2}",
            });
        }

        if (violations.Count > 0)
        {
            throw new BusinessRuleException(
                PostingErrors.UnknownObject, "Tax could not be determined.", violations);
        }
    }

    // ------------------------------------------------------------- balancing

    private static List<CurrencyTotal> CheckBalanced(
        List<WorkingLine> lines, PostingContext context)
    {
        if (lines.Count < 2)
        {
            throw new BusinessRuleException(
                PostingErrors.Unbalanced, "A document needs at least two lines.");
        }

        var totals = new List<CurrencyTotal>
        {
            Total(context.DocumentCurrency.Code, lines.Select(l => l.DocumentAmount)),
            Total(context.LocalCurrency.Code, lines.Select(l => l.LocalAmount)),
        };

        var unbalanced = totals.Where(t => t.Difference != 0).ToList();
        if (unbalanced.Count == 0)
        {
            return totals;
        }

        throw new BusinessRuleException(
            PostingErrors.Unbalanced,
            "Debits do not equal credits.",
            unbalanced.Select(t => new RuleViolation(
                "lines",
                PostingErrors.Unbalanced,
                $"{t.Currency}: debit {t.Debit:N2}, credit {t.Credit:N2}, difference {t.Difference:N2}."))
                .ToList());

        static CurrencyTotal Total(string currency, IEnumerable<decimal> amounts)
        {
            var values = amounts.ToList();
            var debit = values.Where(a => a > 0).Sum();
            var credit = -values.Where(a => a < 0).Sum();
            return new CurrencyTotal(currency, debit, credit, debit - credit);
        }
    }

    // ----------------------------------------------------------------- write

    private JournalEntryHeader WriteDocument(
        PostJournalEntryCommand command, PostingContext context, FiscalPeriodResult period,
        List<WorkingLine> lines, long documentNumber, string formatted, JournalStatus status)
    {
        var parked = status != JournalStatus.Posted;

        var header = new JournalEntryHeader
        {
            TenantId = tenant.TenantId,
            CompanyCodeId = context.CompanyCode.Id,
            FiscalYear = period.FiscalYear,
            DocumentNumber = documentNumber,
            DocumentNumberFormatted = formatted,
            DocumentTypeId = context.DocumentType.Id,
            DocumentDate = command.DocumentDate,
            PostingDate = command.PostingDate,
            FiscalPeriod = period.Period,
            EntryDateUtc = clock.UtcNow,
            DocumentCurrencyId = context.DocumentCurrency.Id,
            ExchangeRateTypeId = context.ExchangeRateTypeId,
            ExchangeRateToLocal = context.RateToLocal,
            ExchangeRateToGroup = 1m,
            Reference = command.Reference,
            HeaderText = command.HeaderText,
            Status = status,
            SourceModule = SourceModule.GeneralLedger,
            TransactionCode = parked ? "FV50" : "FB50",
            IdempotencyKey = command.IdempotencyKey,
            CorrelationId = correlation.CorrelationId,
            CreatedBy = user.UserName,
            CreatedAtUtc = clock.UtcNow,
            // Left unset while parked. "Posted by" on a document that is not in
            // the ledger would be a false statement in the audit trail.
            PostedBy = parked ? null : user.UserName,
            PostedAtUtc = parked ? null : clock.UtcNow,
        };

        foreach (var line in lines)
        {
            header.Lines.Add(new JournalEntryLine
            {
                TenantId = tenant.TenantId,
                CompanyCodeId = context.CompanyCode.Id,
                FiscalYear = period.FiscalYear,
                DocumentNumber = documentNumber,
                LedgerId = context.Ledger.Id,
                LineNumber = line.LineNumber,
                PostingKey = line.PostingKey,
                DebitCredit = line.DebitCredit,
                AccountType = line.AccountType,
                GLAccountId = line.GLAccountId,
                BusinessPartnerId = line.BusinessPartnerId,
                BusinessPartnerRole = line.BusinessPartnerRole,
                CostCenterId = line.CostCenterId,
                ProfitCenterId = line.ProfitCenterId,
                SegmentId = line.SegmentId,
                PartnerCompanyCodeId = line.PartnerCompanyCodeId,
                DocumentAmount = line.DocumentAmount,
                DocumentCurrencyId = context.DocumentCurrency.Id,
                LocalAmount = line.LocalAmount,
                LocalCurrencyId = context.LocalCurrency.Id,
                TaxCode = line.TaxCode,
                TaxBaseAmount = line.TaxBaseAmount,
                TaxAmount = line.TaxAmount,
                IsTaxLine = line.IsTaxLine,
                Assignment = line.Assignment,
                LineText = line.LineText,
                DueDate = line.DueDate,
                BaselineDate = line.DueDate is null ? null : command.PostingDate,
                PaymentTerms = line.PaymentTerms,
                SourceModule = SourceModule.GeneralLedger,
            });
        }

        db.Add(header);
        return header;
    }

    private async Task WriteOpenItemsAsync(
        JournalEntryHeader header, List<WorkingLine> lines,
        PostingContext context, CancellationToken ct)
    {
        foreach (var line in lines.Where(l => l.IsOpenItemManaged))
        {
            db.Add(new OpenItem
            {
                TenantId = tenant.TenantId,
                CompanyCodeId = header.CompanyCodeId,
                FiscalYear = header.FiscalYear,
                DocumentNumber = header.DocumentNumber,
                LedgerId = context.Ledger.Id,
                LineNumber = line.LineNumber,
                AccountType = line.AccountType,
                BusinessPartnerId = line.BusinessPartnerId,
                BusinessPartnerRole = line.BusinessPartnerRole,
                GLAccountId = line.GLAccountId,
                OriginalAmountDocument = line.DocumentAmount,
                OpenAmountDocument = line.DocumentAmount,
                DocumentCurrencyId = context.DocumentCurrency.Id,
                OriginalAmountLocal = line.LocalAmount,
                OpenAmountLocal = line.LocalAmount,
                LocalCurrencyId = context.LocalCurrency.Id,
                DueDate = line.DueDate,
                PaymentTerms = line.PaymentTerms,
                ClearingStatus = ClearingStatus.Open,
            });
        }

        await Task.CompletedTask;
    }

    private void WriteAudit(
        PostingContext context, FiscalPeriodResult period, long documentNumber, string formatted,
        bool parked)
    {
        db.Add(new Audit.Domain.AuditLog
        {
            TenantId = tenant.TenantId,
            OccurredAtUtc = clock.UtcNow,
            UserName = user.UserName,
            CompanyCodeId = context.CompanyCode.Id,
            Action = parked ? Audit.Domain.AuditAction.Park : Audit.Domain.AuditAction.Post,
            ObjectType = "JournalEntry",
            ObjectId = formatted,
            SourceApi = parked
                ? "POST /api/v1/finance/journal-entries/park"
                : "POST /api/v1/finance/journal-entries",
            TransactionCode = parked ? "FV50" : "FB50",
            CorrelationId = correlation.CorrelationId,
            Summary = $"{(parked ? "Parked" : "Posted")} document {documentNumber} " +
                      $"in period {period.Period:D2}/{period.FiscalYear}.",
        });
    }

    // ------------------------------------------------------------ idempotency

    private async Task<PostJournalEntryResult?> FindReplayAsync(
        string key, CancellationToken ct)
    {
        var existing = await db.Set<PostingIdempotency>()
            .SingleOrDefaultAsync(i => i.IdempotencyKey == key, ct);

        if (existing is null)
        {
            return null;
        }

        var header = await db.Set<JournalEntryHeader>()
            .Include(h => h.CompanyCode)
            .SingleAsync(h => h.CompanyCodeId == existing.CompanyCodeId
                              && h.FiscalYear == existing.FiscalYear
                              && h.DocumentNumber == existing.DocumentNumber, ct);

        return new PostJournalEntryResult
        {
            // The replay reports what the document actually is now, not what the
            // replayed call intended: a parked document replays as parked.
            Posted = header.Status == JournalStatus.Posted,
            Status = header.Status.ToString(),
            WasReplay = true,
            CompanyCode = header.CompanyCode.Code,
            FiscalYear = header.FiscalYear,
            FiscalPeriod = header.FiscalPeriod,
            DocumentNumber = header.DocumentNumber,
            DocumentNumberFormatted = header.DocumentNumberFormatted,
            Lines = [],
            Totals = [],
        };
    }
}
