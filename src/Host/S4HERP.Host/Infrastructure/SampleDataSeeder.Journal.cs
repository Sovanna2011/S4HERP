using Microsoft.EntityFrameworkCore;
using S4HERP.Finance.Domain;
using S4HERP.Organization.Domain;

namespace S4HERP.Host.Infrastructure;

public partial class SampleDataSeeder
{
    /// <summary>
    /// Seeds posted documents directly. The posting engine arrives in Phase 3;
    /// these exist so the journal schema, its check constraints and the
    /// reconciliation queries have real data to run against — including a
    /// foreign-currency document and an intercompany pair.
    ///
    /// Amounts are signed: debit positive, credit negative, so a balanced
    /// document sums to zero in every currency.
    /// </summary>
    private async Task SeedJournalAsync(
        OrgIds orgs, ChartOfAccountsIds coa, CoIds co, PartnerIds partners, CancellationToken ct)
    {
        var usd = await CurrencyId("USD", ct);
        var khr = await CurrencyId("KHR", ct);
        var thb = await CurrencyId("THB", ct);

        var documentTypes = await db.Set<DocumentType>()
            .Where(x => x.TenantId == _tenantId).ToDictionaryAsync(x => x.Code, x => x.Id, ct);
        var rateTypeId = await db.Set<ExchangeRateType>()
            .Where(x => x.TenantId == _tenantId && x.Code == "M").Select(x => x.Id).SingleAsync(ct);

        var cc1000 = orgs.CompanyCodes["1000"];
        var cc2000 = orgs.CompanyCodes["2000"];
        var ledger = coa.LeadingLedgerId;
        var postingDate = new DateOnly(SeedFiscalYear, 3, 15);

        // ---- 1. Customer invoice in USD (local currency) --------------------
        var invoice = NewHeader(
            cc1000, "1000", 200000001, "DR", documentTypes, usd, rateTypeId, postingDate,
            "INV-2026-0001", "Sale of refined sugar", SourceModule.AccountsReceivable);
        db.Add(invoice);

        AddLine(invoice, ledger, 1, "01", DebitCredit.Debit, AccountType.Customer,
            amount: 1100m, usd, usd, 1m,
            partnerId: partners.Customer, role: "FI_CUST",
            dueDate: postingDate.AddDays(30));
        AddLine(invoice, ledger, 2, "50", DebitCredit.Credit, AccountType.GeneralLedger,
            amount: -1000m, usd, usd, 1m,
            glAccountId: coa.Accounts["4000000000"],
            profitCenterId: co.ProfitCenters["PC1000"], segmentId: orgs.Segments["SUGAR"]);
        AddLine(invoice, ledger, 3, "50", DebitCredit.Credit, AccountType.GeneralLedger,
            amount: -100m, usd, usd, 1m,
            glAccountId: coa.Accounts["2200000000"], taxCode: "A1", isTaxLine: true);

        // ---- 2. Vendor invoice in KHR, local USD (foreign currency) ---------
        // 4,100,000 KHR at 0.000244 USD/KHR = 1,000.40 USD.
        const decimal khrToUsd = 0.000244m;
        var fxInvoice = NewHeader(
            cc1000, "1000", 400000001, "KR", documentTypes, khr, rateTypeId, postingDate,
            "VINV-2026-0001", "Inland haulage, Mekong Logistics", SourceModule.AccountsPayable,
            rateToLocal: khrToUsd);
        db.Add(fxInvoice);

        AddLine(fxInvoice, ledger, 1, "40", DebitCredit.Debit, AccountType.GeneralLedger,
            amount: 4_000_000m, khr, usd, khrToUsd,
            glAccountId: coa.Accounts["6000000000"],
            costCenterId: co.CostCenters["CC102000"],
            profitCenterId: co.ProfitCenters["PC2000"], taxCode: "V1");
        AddLine(fxInvoice, ledger, 2, "40", DebitCredit.Debit, AccountType.GeneralLedger,
            amount: 400_000m, khr, usd, khrToUsd,
            glAccountId: coa.Accounts["1300000000"], taxCode: "V1", isTaxLine: true);
        AddLine(fxInvoice, ledger, 3, "31", DebitCredit.Credit, AccountType.Vendor,
            amount: -4_400_000m, khr, usd, khrToUsd,
            partnerId: partners.Vendor, role: "FI_VEND",
            dueDate: postingDate.AddDays(30));

        // ---- 3/4. Intercompany pair: 1000 pays an expense for 2000 ----------
        // Each legal entity gets its own document from its own number range; both
        // are written in one transaction and linked by IntercompanyTransactionId.
        var intercompanyId = Guid.NewGuid();
        const decimal usdToThb = 36.5m;

        var icSender = NewHeader(
            cc1000, "1000", 700000001, "IC", documentTypes, usd, rateTypeId, postingDate,
            "IC-2026-0001", "Shared service recharge to KSS Thailand",
            SourceModule.GeneralLedger, intercompanyTransactionId: intercompanyId);
        db.Add(icSender);

        AddLine(icSender, ledger, 1, "40", DebitCredit.Debit, AccountType.GeneralLedger,
            amount: 5000m, usd, usd, 1m,
            glAccountId: coa.Accounts["1900000000"],
            partnerCompanyCodeId: cc2000, profitCenterId: co.ProfitCenters["PC9000"]);
        AddLine(icSender, ledger, 2, "50", DebitCredit.Credit, AccountType.GeneralLedger,
            amount: -5000m, usd, usd, 1m,
            glAccountId: coa.Accounts["1000100000"]);

        // Receiver books in THB, its local currency. Group amounts agree in USD.
        var icReceiver = NewHeader(
            cc2000, "2000", 700000001, "IC", documentTypes, usd, rateTypeId, postingDate,
            "IC-2026-0001", "Shared service recharge from KSS Cambodia",
            SourceModule.GeneralLedger, rateToLocal: usdToThb,
            intercompanyTransactionId: intercompanyId);
        db.Add(icReceiver);

        AddLine(icReceiver, ledger, 1, "40", DebitCredit.Debit, AccountType.GeneralLedger,
            amount: 5000m, usd, thb, usdToThb,
            glAccountId: coa.Accounts["6000000000"],
            costCenterId: co.CostCenters["CC200100"],
            partnerCompanyCodeId: cc1000, profitCenterId: co.ProfitCenters["PC9000"]);
        AddLine(icReceiver, ledger, 2, "50", DebitCredit.Credit, AccountType.GeneralLedger,
            amount: -5000m, usd, thb, usdToThb,
            glAccountId: coa.Accounts["2900000000"], partnerCompanyCodeId: cc1000);

        await db.SaveChangesAsync(ct);

        // Open items for the two open-item-managed subledger lines.
        db.Add(NewOpenItem(invoice, ledger, 1, AccountType.Customer, partners.Customer, "FI_CUST",
            1100m, usd, 1100m, usd, postingDate.AddDays(30)));
        db.Add(NewOpenItem(fxInvoice, ledger, 3, AccountType.Vendor, partners.Vendor, "FI_VEND",
            -4_400_000m, khr, -1073.60m, usd, postingDate.AddDays(30)));

        await db.SaveChangesAsync(ct);
    }

    private JournalEntryHeader NewHeader(
        long companyCodeId, string companyCode, long documentNumber, string documentType,
        Dictionary<string, long> documentTypes, long currencyId, long rateTypeId,
        DateOnly postingDate, string reference, string headerText, SourceModule source,
        decimal rateToLocal = 1m, Guid? intercompanyTransactionId = null) => new()
    {
        TenantId = _tenantId,
        CompanyCodeId = companyCodeId,
        FiscalYear = SeedFiscalYear,
        DocumentNumber = documentNumber,
        DocumentNumberFormatted =
            $"KSS-{companyCode}-{SeedFiscalYear}-{documentType}-{documentNumber:D10}",
        DocumentTypeId = documentTypes[documentType],
        DocumentDate = postingDate,
        PostingDate = postingDate,
        FiscalPeriod = (byte)postingDate.Month,
        EntryDateUtc = DateTime.UtcNow,
        DocumentCurrencyId = currencyId,
        ExchangeRateTypeId = rateTypeId,
        ExchangeRateToLocal = rateToLocal,
        ExchangeRateToGroup = 1m,
        Reference = reference,
        HeaderText = headerText,
        Status = JournalStatus.Posted,
        SourceModule = source,
        IntercompanyTransactionId = intercompanyTransactionId,
        PostedBy = "SEED",
        PostedAtUtc = DateTime.UtcNow,
        CreatedBy = "SEED",
    };

    private static void AddLine(
        JournalEntryHeader header, long ledgerId, short lineNumber, string postingKey,
        DebitCredit debitCredit, AccountType accountType, decimal amount,
        long documentCurrencyId, long localCurrencyId, decimal rateToLocal,
        long? glAccountId = null, long? partnerId = null, string? role = null,
        long? costCenterId = null, long? profitCenterId = null, long? segmentId = null,
        long? partnerCompanyCodeId = null, string? taxCode = null, bool isTaxLine = false,
        DateOnly? dueDate = null)
    {
        header.Lines.Add(new JournalEntryLine
        {
            TenantId = header.TenantId,
            CompanyCodeId = header.CompanyCodeId,
            FiscalYear = header.FiscalYear,
            DocumentNumber = header.DocumentNumber,
            LedgerId = ledgerId,
            LineNumber = lineNumber,
            PostingKey = postingKey,
            DebitCredit = debitCredit,
            AccountType = accountType,
            GLAccountId = glAccountId,
            BusinessPartnerId = partnerId,
            BusinessPartnerRole = role,
            CostCenterId = costCenterId,
            ProfitCenterId = profitCenterId,
            SegmentId = segmentId,
            PartnerCompanyCodeId = partnerCompanyCodeId,
            DocumentAmount = amount,
            DocumentCurrencyId = documentCurrencyId,
            LocalAmount = decimal.Round(amount * rateToLocal, 4),
            LocalCurrencyId = localCurrencyId,
            TaxCode = taxCode,
            IsTaxLine = isTaxLine,
            DueDate = dueDate,
            BaselineDate = dueDate is null ? null : header.PostingDate,
            LineText = header.HeaderText,
            SourceModule = header.SourceModule,
        });
    }

    private OpenItem NewOpenItem(
        JournalEntryHeader header, long ledgerId, short lineNumber, AccountType accountType,
        long partnerId, string role, decimal documentAmount, long documentCurrencyId,
        decimal localAmount, long localCurrencyId, DateOnly dueDate) => new()
    {
        TenantId = _tenantId,
        CompanyCodeId = header.CompanyCodeId,
        FiscalYear = header.FiscalYear,
        DocumentNumber = header.DocumentNumber,
        LedgerId = ledgerId,
        LineNumber = lineNumber,
        AccountType = accountType,
        BusinessPartnerId = partnerId,
        BusinessPartnerRole = role,
        OriginalAmountDocument = documentAmount,
        OpenAmountDocument = documentAmount,
        DocumentCurrencyId = documentCurrencyId,
        OriginalAmountLocal = localAmount,
        OpenAmountLocal = localAmount,
        LocalCurrencyId = localCurrencyId,
        DueDate = dueDate,
        PaymentTerms = "N030",
        ClearingStatus = ClearingStatus.Open,
    };
}
