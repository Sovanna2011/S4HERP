using Microsoft.EntityFrameworkCore;
using S4HERP.BusinessPartner.Domain;
using S4HERP.Controlling.Domain;
using S4HERP.Finance.Domain;
using S4HERP.Organization.Domain;

namespace S4HERP.Host.Infrastructure;

/// <summary>
/// The §24 sample: two companies, three company codes, one shared chart of
/// accounts, one controlling area, USD group currency, USD/KHR/THB transaction
/// currencies, dual-role business partners, cost and profit centres, internal
/// orders, and intercompany and foreign-currency documents.
///
/// Idempotent: every step checks for its natural key first, so the seeder is
/// safe to re-run on every start.
/// </summary>
public partial class SampleDataSeeder
{
    private const string TenantCode = "KSS";
    private const short SeedFiscalYear = 2026;

    private long _tenantId;

    private async Task SeedCoreAsync(CancellationToken ct)
    {
        if (await db.Set<Tenant>().AnyAsync(t => t.Code == TenantCode, ct))
        {
            logger.LogInformation("Sample data already present; skipping seed.");
            return;
        }

        logger.LogInformation("Seeding sample data.");

        await SeedTenantAndCurrenciesAsync(ct);
        var calendars = await SeedCalendarAsync(ct);
        var coa = await SeedChartOfAccountsAsync(ct);
        var orgs = await SeedOrganizationAsync(calendars, coa, ct);
        await SeedDocumentConfigurationAsync(orgs, coa, ct);
        var co = await SeedControllingAsync(orgs, calendars, coa, ct);
        var partners = await SeedBusinessPartnersAsync(orgs, coa, ct);
        await SeedJournalAsync(orgs, coa, co, partners, ct);
        await SeedSecurityAsync(orgs, ct);

        logger.LogInformation("Sample data seeded.");
    }

    // ---------------------------------------------------------------- tenant

    private async Task SeedTenantAndCurrenciesAsync(CancellationToken ct)
    {
        var tenant = new Tenant
        {
            Code = TenantCode, Name = "KSS Group", DefaultLanguage = "en", CreatedBy = "SEED",
        };
        db.Add(tenant);
        await db.SaveChangesAsync(ct);
        _tenantId = tenant.Id;

        db.AddRange(
            Currency("USD", "US Dollar", 2),
            Currency("KHR", "Cambodian Riel", 0),
            Currency("THB", "Thai Baht", 2));

        db.Add(new ExchangeRateType
        {
            TenantId = _tenantId, Code = "M", Name = "Standard translation at average rate",
            QuotationDirection = QuotationDirection.Direct, CreatedBy = "SEED",
        });
        await db.SaveChangesAsync(ct);

        var usd = await CurrencyId("USD", ct);
        var khr = await CurrencyId("KHR", ct);
        var thb = await CurrencyId("THB", ct);
        var rateType = await db.Set<ExchangeRateType>()
            .Where(x => x.TenantId == _tenantId && x.Code == "M").Select(x => x.Id).SingleAsync(ct);

        var from = new DateOnly(SeedFiscalYear, 1, 1);
        db.AddRange(
            Rate(rateType, khr, usd, 0.000244m, from),
            Rate(rateType, usd, khr, 4100.000000m, from),
            Rate(rateType, thb, usd, 0.027400m, from),
            Rate(rateType, usd, thb, 36.500000m, from));
        await db.SaveChangesAsync(ct);
    }

    private Currency Currency(string code, string name, byte decimals) => new()
    {
        TenantId = _tenantId, Code = code, Name = name, DecimalPlaces = decimals, CreatedBy = "SEED",
    };

    private ExchangeRate Rate(long type, long fromCurrency, long toCurrency, decimal rate, DateOnly from) => new()
    {
        TenantId = _tenantId, ExchangeRateTypeId = type, FromCurrencyId = fromCurrency,
        ToCurrencyId = toCurrency, Rate = rate, ValidFrom = from, CreatedBy = "SEED",
    };

    private Task<long> CurrencyId(string code, CancellationToken ct) => db.Set<Currency>()
        .Where(x => x.TenantId == _tenantId && x.Code == code).Select(x => x.Id).SingleAsync(ct);

    // -------------------------------------------------------------- calendar

    private sealed record Calendars(long FiscalYearVariantId, long PostingPeriodVariantId);

    private async Task<Calendars> SeedCalendarAsync(CancellationToken ct)
    {
        var fyv = new FiscalYearVariant
        {
            TenantId = _tenantId, Code = "K4", Name = "Calendar year, 12 periods + 4 special",
            NormalPeriods = 12, SpecialPeriods = 4, IsCalendarYear = true, CreatedBy = "SEED",
        };
        var ppv = new PostingPeriodVariant
        {
            TenantId = _tenantId, Code = "KSS1", Name = "KSS posting periods", CreatedBy = "SEED",
        };
        db.AddRange(fyv, ppv);
        await db.SaveChangesAsync(ct);

        // Two fiscal years so year-end behaviour has something to act on.
        foreach (var year in new short[] { SeedFiscalYear, (short)(SeedFiscalYear + 1) })
        {
            for (byte period = 1; period <= 12; period++)
            {
                var start = new DateOnly(year, period, 1);
                db.Add(new FiscalYearPeriod
                {
                    TenantId = _tenantId, FiscalYearVariantId = fyv.Id, FiscalYear = year,
                    Period = period, StartDate = start, EndDate = start.AddMonths(1).AddDays(-1),
                    CreatedBy = "SEED",
                });
            }

            // Special periods share the last day of the year for adjustments.
            var yearEnd = new DateOnly(year, 12, 31);
            for (byte period = 13; period <= 16; period++)
            {
                db.Add(new FiscalYearPeriod
                {
                    TenantId = _tenantId, FiscalYearVariantId = fyv.Id, FiscalYear = year,
                    Period = period, StartDate = yearEnd, EndDate = yearEnd,
                    IsSpecialPeriod = true, CreatedBy = "SEED",
                });
            }

            foreach (var accountType in new[]
                     {
                         AccountType.GeneralLedger, AccountType.Customer,
                         AccountType.Vendor, AccountType.Asset,
                     })
            {
                for (byte period = 1; period <= 16; period++)
                {
                    db.Add(new PostingPeriod
                    {
                        TenantId = _tenantId, PostingPeriodVariantId = ppv.Id,
                        AccountType = accountType, FiscalYear = year, Period = period,
                        IsOpen = year == SeedFiscalYear && period <= 12,
                        CreatedBy = "SEED",
                    });
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return new Calendars(fyv.Id, ppv.Id);
    }

    // ------------------------------------------------------ chart of accounts

    private sealed record ChartOfAccountsIds(
        long ChartId, long LeadingLedgerId, long IfrsLedgerId, Dictionary<string, long> Accounts);

    private async Task<ChartOfAccountsIds> SeedChartOfAccountsAsync(CancellationToken ct)
    {
        var chart = new ChartOfAccounts
        {
            TenantId = _tenantId, Code = "INT", Name = "International chart of accounts",
            MaintenanceLanguage = "en", CreatedBy = "SEED",
        };
        var leading = new Ledger
        {
            TenantId = _tenantId, Code = "0L", Name = "Leading ledger (local GAAP)",
            LedgerType = LedgerType.Leading, AccountingStandard = "LOCAL", CreatedBy = "SEED",
        };
        var ifrs = new Ledger
        {
            TenantId = _tenantId, Code = "2I", Name = "IFRS ledger",
            LedgerType = LedgerType.NonLeading, AccountingStandard = "IFRS", CreatedBy = "SEED",
        };
        db.AddRange(chart, leading, ifrs);
        await db.SaveChangesAsync(ct);

        var groups = new (string Code, string Name, string From, string To)[]
        {
            ("ASST", "Assets", "1000000000", "1999999999"),
            ("LIAB", "Liabilities and equity", "2000000000", "2999999999"),
            ("REVN", "Revenue", "4000000000", "4999999999"),
            ("EXPN", "Expenses", "5000000000", "6999999999"),
        };

        var groupIds = new Dictionary<string, long>();
        foreach (var g in groups)
        {
            var group = new GLAccountGroup
            {
                TenantId = _tenantId, ChartOfAccountsId = chart.Id, Code = g.Code, Name = g.Name,
                FromAccount = g.From, ToAccount = g.To, CreatedBy = "SEED",
            };
            db.Add(group);
            await db.SaveChangesAsync(ct);
            groupIds[g.Code] = group.Id;
        }

        var accounts = new (string Number, string Group, string Short, string Long,
            GLAccountType Type, bool Recon, AccountType? ReconType, bool Retained)[]
        {
            ("1000100000", "ASST", "Bank USD", "Bank current account USD", GLAccountType.BalanceSheet, false, null, false),
            ("1000200000", "ASST", "Bank KHR", "Bank current account KHR", GLAccountType.BalanceSheet, false, null, false),
            ("1200000000", "ASST", "Trade AR", "Trade accounts receivable", GLAccountType.BalanceSheet, true, AccountType.Customer, false),
            ("1300000000", "ASST", "Input VAT", "Input VAT recoverable", GLAccountType.BalanceSheet, false, null, false),
            ("1500000000", "ASST", "Fixed assets", "Fixed assets at cost", GLAccountType.BalanceSheet, true, AccountType.Asset, false),
            ("1900000000", "ASST", "IC receivable", "Intercompany receivable", GLAccountType.BalanceSheet, false, null, false),
            ("2100000000", "LIAB", "Trade AP", "Trade accounts payable", GLAccountType.BalanceSheet, true, AccountType.Vendor, false),
            ("2200000000", "LIAB", "Output VAT", "Output VAT payable", GLAccountType.BalanceSheet, false, null, false),
            ("2300000000", "LIAB", "WHT payable", "Withholding tax payable", GLAccountType.BalanceSheet, false, null, false),
            ("2900000000", "LIAB", "IC payable", "Intercompany payable", GLAccountType.BalanceSheet, false, null, false),
            ("2950000000", "LIAB", "Retained earn", "Retained earnings brought forward", GLAccountType.BalanceSheet, false, null, true),
            ("4000000000", "REVN", "Revenue", "Revenue from sales of goods", GLAccountType.ProfitAndLoss, false, null, false),
            ("5000000000", "EXPN", "COGS", "Cost of goods sold", GLAccountType.ProfitAndLoss, false, null, false),
            ("6000000000", "EXPN", "Admin expense", "Administrative expenses", GLAccountType.ProfitAndLoss, false, null, false),
            ("6100000000", "EXPN", "Depreciation", "Depreciation expense", GLAccountType.ProfitAndLoss, false, null, false),
            ("6900000000", "EXPN", "FX gain/loss", "Realised foreign exchange gain or loss", GLAccountType.ProfitAndLoss, false, null, false),
            ("6910000000", "EXPN", "Rounding", "Rounding differences", GLAccountType.ProfitAndLoss, false, null, false),
        };

        foreach (var a in accounts)
        {
            db.Add(new GLAccount
            {
                TenantId = _tenantId, ChartOfAccountsId = chart.Id,
                GLAccountGroupId = groupIds[a.Group], AccountNumber = a.Number,
                ShortText = a.Short, LongText = a.Long, AccountType = a.Type,
                IsReconciliationAccount = a.Recon, ReconciliationAccountType = a.ReconType,
                IsRetainedEarningsAccount = a.Retained, CreatedBy = "SEED",
            });
        }

        await db.SaveChangesAsync(ct);

        var map = await db.Set<GLAccount>()
            .Where(x => x.TenantId == _tenantId && x.ChartOfAccountsId == chart.Id)
            .ToDictionaryAsync(x => x.AccountNumber, x => x.Id, ct);

        return new ChartOfAccountsIds(chart.Id, leading.Id, ifrs.Id, map);
    }
}
