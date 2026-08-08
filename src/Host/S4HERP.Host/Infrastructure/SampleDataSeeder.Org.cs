using Microsoft.EntityFrameworkCore;
using S4HERP.Controlling.Domain;
using S4HERP.Finance.Domain;
using S4HERP.Organization.Domain;

namespace S4HERP.Host.Infrastructure;

public partial class SampleDataSeeder
{
    private sealed record OrgIds(
        Dictionary<string, long> CompanyCodes,
        long CreditControlAreaId,
        long SalesOrganizationId,
        long PurchasingOrganizationId,
        Dictionary<string, long> Segments);

    private async Task<OrgIds> SeedOrganizationAsync(
        Calendars calendars, ChartOfAccountsIds coa, CancellationToken ct)
    {
        var usd = await CurrencyId("USD", ct);
        var thb = await CurrencyId("THB", ct);

        var segments = new[] { ("SUGAR", "Sugar operations"), ("TRADE", "Trading"), ("CORP", "Corporate") };
        foreach (var (code, name) in segments)
        {
            db.Add(new Segment { TenantId = _tenantId, Code = code, Name = name, CreatedBy = "SEED" });
        }

        foreach (var (code, name) in new[] { ("0100", "Production"), ("0200", "Sales and distribution"), ("0300", "Administration") })
        {
            db.Add(new FunctionalArea { TenantId = _tenantId, Code = code, Name = name, CreatedBy = "SEED" });
        }

        foreach (var (code, name) in new[] { ("1000", "Sugar"), ("2000", "Trading") })
        {
            db.Add(new BusinessArea { TenantId = _tenantId, Code = code, Name = name, CreatedBy = "SEED" });
        }

        var creditControlArea = new CreditControlArea
        {
            TenantId = _tenantId, Code = "CC01", Name = "KSS credit control", CurrencyId = usd,
            CreatedBy = "SEED",
        };
        db.Add(creditControlArea);
        await db.SaveChangesAsync(ct);

        var companies = new[]
        {
            new Company
            {
                TenantId = _tenantId, Code = "1000", Name = "KSS Group", CountryCode = "KH",
                GroupCurrencyId = usd, CreatedBy = "SEED",
            },
            new Company
            {
                TenantId = _tenantId, Code = "2000", Name = "KSS Thailand Holding", CountryCode = "TH",
                GroupCurrencyId = usd, CreatedBy = "SEED",
            },
        };
        db.AddRange(companies);
        await db.SaveChangesAsync(ct);

        // Company code 2000 keeps a THB local currency inside a USD controlling
        // area, so translation is exercised by the seed rather than discovered later.
        var companyCodes = new[]
        {
            NewCompanyCode("1000", "KSS Cambodia", "KH", companies[0].Id, usd, calendars, coa, creditControlArea.Id),
            NewCompanyCode("1100", "KSS Kampot", "KH", companies[0].Id, usd, calendars, coa, creditControlArea.Id),
            NewCompanyCode("2000", "KSS Thailand", "TH", companies[1].Id, thb, calendars, coa, null),
        };
        db.AddRange(companyCodes);
        await db.SaveChangesAsync(ct);

        var codeMap = companyCodes.ToDictionary(x => x.Code, x => x.Id);

        db.Add(new Plant
        {
            TenantId = _tenantId, Code = "1010", Name = "Kampot mill",
            CompanyCodeId = codeMap["1100"], City = "Kampot", CreatedBy = "SEED",
        });
        db.Add(new Branch
        {
            TenantId = _tenantId, Code = "100100", Name = "Phnom Penh head office",
            CompanyCodeId = codeMap["1000"], City = "Phnom Penh", CreatedBy = "SEED",
        });

        var salesOrg = new SalesOrganization
        {
            TenantId = _tenantId, Code = "1000", Name = "KSS Cambodia sales",
            CompanyCodeId = codeMap["1000"], CurrencyId = usd, CreatedBy = "SEED",
        };
        var purchasingOrg = new PurchasingOrganization
        {
            TenantId = _tenantId, Code = "1000", Name = "KSS Cambodia purchasing",
            CompanyCodeId = codeMap["1000"], CreatedBy = "SEED",
        };
        db.AddRange(salesOrg, purchasingOrg);

        // Account settings per company code. Reconciliation accounts and the bank
        // accounts are open-item managed.
        var openItemAccounts = new[] { "1200000000", "2100000000", "1000100000", "1000200000" };
        foreach (var companyCode in companyCodes)
        {
            var localCurrency = companyCode.LocalCurrencyId;
            foreach (var (number, accountId) in coa.Accounts)
            {
                db.Add(new GLAccountCompanyCode
                {
                    TenantId = _tenantId, GLAccountId = accountId, CompanyCodeId = companyCode.Id,
                    AccountCurrencyId = localCurrency,
                    IsOpenItemManaged = openItemAccounts.Contains(number),
                    IsTaxRelevant = number is "1300000000" or "2200000000",
                    RequiresCostObject = number.StartsWith('6') || number.StartsWith('5'),
                    CreatedBy = "SEED",
                });
            }
        }

        await db.SaveChangesAsync(ct);

        var segmentMap = await db.Set<Segment>()
            .Where(x => x.TenantId == _tenantId).ToDictionaryAsync(x => x.Code, x => x.Id, ct);

        return new OrgIds(codeMap, creditControlArea.Id, salesOrg.Id, purchasingOrg.Id, segmentMap);
    }

    private CompanyCode NewCompanyCode(
        string code, string name, string country, long companyId, long currencyId,
        Calendars calendars, ChartOfAccountsIds coa, long? creditControlAreaId) => new()
    {
        TenantId = _tenantId, Code = code, Name = name, CountryCode = country, Language = "en",
        CompanyId = companyId, LocalCurrencyId = currencyId, ChartOfAccountsId = coa.ChartId,
        FiscalYearVariantId = calendars.FiscalYearVariantId,
        PostingPeriodVariantId = calendars.PostingPeriodVariantId,
        CreditControlAreaId = creditControlAreaId, CreatedBy = "SEED",
    };

    // ----------------------------------------------- document configuration

    /// <summary>
    /// Payment terms. `N030` was already referenced by the seeded partner facets
    /// as a bare code with nothing behind it; it now exists, and the due dates
    /// derived from it are real rather than whatever a caller supplied.
    /// </summary>
    private void SeedPaymentTerms()
    {
        var terms = new (string Code, string Name, int Net, int? D1, decimal? P1, int? D2, decimal? P2)[]
        {
            ("N000", "Due immediately", 0, null, null, null, null),
            ("N014", "Net 14 days", 14, null, null, null, null),
            ("N030", "Net 30 days", 30, null, null, null, null),
            ("N060", "Net 60 days", 60, null, null, null, null),
            // The classic two-tier discount, and the reason CashDiscountPercentOn
            // takes the tiers in order: on day 5 the 2% applies, not the 1%.
            ("D210", "2% 10 days, 1% 20 days, net 30", 30, 10, 2m, 20, 1m),
        };

        foreach (var t in terms)
        {
            db.Add(new PaymentTerm
            {
                TenantId = _tenantId, Code = t.Code, Name = t.Name,
                BaselineDateRule = BaselineDateRule.DocumentDate,
                NetDays = t.Net,
                CashDiscount1Days = t.D1, CashDiscount1Percent = t.P1,
                CashDiscount2Days = t.D2, CashDiscount2Percent = t.P2,
                CreatedBy = "SEED",
            });
        }
    }

    /// <summary>
    /// Payment methods. Like the terms, `PartnerCompanyCode.PaymentMethods` has
    /// carried these single-character codes since Phase 2 with nothing behind
    /// them; the payment run selects by method, so they have to exist.
    /// </summary>
    private void SeedPaymentMethods()
    {
        var methods = new (string Code, string Name, PaymentDirection Dir, bool Bank)[]
        {
            ("T", "Bank transfer (outgoing)", PaymentDirection.Outgoing, true),
            ("C", "Cheque (outgoing)", PaymentDirection.Outgoing, false),
            ("I", "Incoming transfer", PaymentDirection.Incoming, true),
        };

        foreach (var m in methods)
        {
            db.Add(new PaymentMethod
            {
                TenantId = _tenantId, Code = m.Code, Name = m.Name,
                Direction = m.Dir, RequiresBankDetails = m.Bank, CreatedBy = "SEED",
            });
        }
    }

    private async Task SeedDocumentConfigurationAsync(
        OrgIds orgs, ChartOfAccountsIds coa, CancellationToken ct)
    {
        SeedPaymentTerms();
        SeedPaymentMethods();

        var documentTypes = new (string Code, string Name, string Range, string Types, bool Ic)[]
        {
            ("SA", "G/L account document", "01", "S", false),
            ("DR", "Customer invoice", "02", "SD", false),
            ("DZ", "Customer payment", "03", "SD", false),
            ("KR", "Vendor invoice", "04", "SK", false),
            ("KZ", "Vendor payment", "05", "SK", false),
            ("AB", "Accounting document", "01", "SDKA", false),
            ("AA", "Asset posting", "06", "SA", false),
            ("IC", "Intercompany document", "07", "S", true),
        };

        foreach (var t in documentTypes)
        {
            db.Add(new DocumentType
            {
                TenantId = _tenantId, Code = t.Code, Name = t.Name, NumberRangeCode = t.Range,
                AllowedAccountTypes = t.Types, IsIntercompany = t.Ic,
                ReversalDocumentTypeCode = "AB", CreatedBy = "SEED",
            });
        }

        // Non-overlapping intervals per company code and year, which is what makes
        // the numeric document number unique within (company code, fiscal year).
        var ranges = new (string Code, long From, long To)[]
        {
            ("01", 100000000, 199999999),
            ("02", 200000000, 299999999),
            ("03", 300000000, 399999999),
            ("04", 400000000, 499999999),
            ("05", 500000000, 599999999),
            ("06", 600000000, 699999999),
            ("07", 700000000, 799999999),
        };

        foreach (var companyCodeId in orgs.CompanyCodes.Values)
        {
            foreach (var year in new short[] { SeedFiscalYear, (short)(SeedFiscalYear + 1) })
            {
                foreach (var r in ranges)
                {
                    db.Add(new NumberRange
                    {
                        TenantId = _tenantId, Code = r.Code,
                        ObjectType = NumberRangeObject.AccountingDocument,
                        CompanyCodeId = companyCodeId, FiscalYear = year,
                        FromNumber = r.From, ToNumber = r.To, CurrentNumber = r.From - 1,
                        IsGapless = true, Prefix = "KSS", PaddingLength = 10, CreatedBy = "SEED",
                    });
                }
            }
        }

        db.Add(new NumberRange
        {
            TenantId = _tenantId, Code = "BP", ObjectType = NumberRangeObject.BusinessPartner,
            FiscalYear = 0, FromNumber = 1000000, ToNumber = 1999999, CurrentNumber = 1000000,
            IsGapless = false, PaddingLength = 10, CreatedBy = "SEED",
        });

        // Payment runs. Not gapless and not per company code: a run identifier is
        // an internal handle, not an accounting document number, so the legal
        // argument for gaplessness does not apply and a discarded proposal may
        // leave a hole. What it must be is unique — the run id used to end in a
        // seconds timestamp, and two proposals created in the same second
        // collided on the unique index and surfaced as a 500.
        db.Add(new NumberRange
        {
            TenantId = _tenantId, Code = "PR", ObjectType = NumberRangeObject.PaymentRun,
            FiscalYear = 0, FromNumber = 1, ToNumber = 999_999, CurrentNumber = 0,
            IsGapless = false, PaddingLength = 6, CreatedBy = "SEED",
        });

        // Bank change requests. Same shape and same reasoning as the payment run
        // range: an internal handle that must be unique, with no legal claim to
        // being gapless.
        db.Add(new NumberRange
        {
            TenantId = _tenantId, Code = "BK", ObjectType = NumberRangeObject.PartnerBankChange,
            FiscalYear = 0, FromNumber = 1, ToNumber = 99_999_999, CurrentNumber = 0,
            IsGapless = false, PaddingLength = 8, CreatedBy = "SEED",
        });

        var postingKeys = new (string Code, string Name, AccountType Type, bool Debit)[]
        {
            ("40", "G/L debit", AccountType.GeneralLedger, true),
            ("50", "G/L credit", AccountType.GeneralLedger, false),
            ("01", "Customer invoice", AccountType.Customer, true),
            ("15", "Customer payment", AccountType.Customer, false),
            ("21", "Vendor credit memo", AccountType.Vendor, true),
            ("31", "Vendor invoice", AccountType.Vendor, false),
            ("70", "Asset debit", AccountType.Asset, true),
            ("75", "Asset credit", AccountType.Asset, false),
        };

        foreach (var pk in postingKeys)
        {
            db.Add(new PostingKey
            {
                TenantId = _tenantId, Code = pk.Code, Name = pk.Name,
                AccountType = pk.Type, IsDebit = pk.Debit, CreatedBy = "SEED",
            });
        }

        var validFrom = new DateOnly(SeedFiscalYear, 1, 1);
        var taxCodes = new (string Code, string Country, string Name, TaxDirection Dir, decimal Rate)[]
        {
            ("V0", "KH", "Input VAT 0%", TaxDirection.Input, 0m),
            ("V1", "KH", "Input VAT 10%", TaxDirection.Input, 10m),
            ("A0", "KH", "Output VAT 0%", TaxDirection.Output, 0m),
            ("A1", "KH", "Output VAT 10%", TaxDirection.Output, 10m),
            ("V7", "TH", "Input VAT 7%", TaxDirection.Input, 7m),
            ("A7", "TH", "Output VAT 7%", TaxDirection.Output, 7m),
        };

        foreach (var tc in taxCodes)
        {
            db.Add(new TaxCode
            {
                TenantId = _tenantId, Code = tc.Code, CountryCode = tc.Country, Name = tc.Name,
                Direction = tc.Dir, Rate = tc.Rate, ValidFrom = validFrom,
                TaxAccountId = tc.Dir == TaxDirection.Input
                    ? coa.Accounts["1300000000"]
                    : coa.Accounts["2200000000"],
                CreatedBy = "SEED",
            });
        }

        foreach (var t in TransactionCodeCatalog)
        {
            db.Add(new TransactionCode
            {
                TenantId = _tenantId, Code = t.Code, Description = t.Description,
                Category = t.Category, Module = t.Module, Target = t.Target,
                ValidFrom = validFrom, CreatedBy = "SEED",
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static readonly (string Code, string Description, string Category, string Module, string Target)[]
        TransactionCodeCatalog =
        [
            ("SPRO", "Configuration workbench", "Configuration", "Organization", "#/config"),
            ("OBY6", "Company code", "Configuration", "Organization", "#/config/company-code"),
            ("OB13", "Chart of accounts", "Configuration", "Finance", "#/config/chart-of-accounts"),
            ("OB52", "Posting periods", "Configuration", "Organization", "#/config/posting-periods"),
            ("FS00", "G/L account master", "Master Data", "Finance", "#/gl/account"),
            ("BP", "Business partner", "Master Data", "BusinessPartner", "#/bp"),
            ("BUP1", "Create business partner", "Master Data", "BusinessPartner", "#/bp/create"),
            ("BUP2", "Change business partner", "Master Data", "BusinessPartner", "#/bp/change"),
            ("BUP3", "Display business partner", "Master Data", "BusinessPartner", "#/bp/display"),
            ("FB50", "Journal entry", "Transaction", "Finance", "#/journal/create"),
            ("FB01", "Post document", "Transaction", "Finance", "#/journal/post"),
            ("FB02", "Change document", "Transaction", "Finance", "#/journal/change"),
            ("FB03", "Display document", "Transaction", "Finance", "#/journal/display"),
            ("FB08", "Reverse document", "Transaction", "Finance", "#/journal/reverse"),
            ("FBL1N", "Vendor line items", "Reporting", "Finance", "#/reports/vendor-items"),
            ("FBL3N", "G/L line items", "Reporting", "Finance", "#/reports/gl-items"),
            ("FBL5N", "Customer line items", "Reporting", "Finance", "#/reports/customer-items"),
            ("F-28", "Incoming payment", "Transaction", "Finance", "#/ar/incoming-payment"),
            ("F-53", "Outgoing payment", "Transaction", "Finance", "#/ap/outgoing-payment"),
            ("F110", "Automatic payment run", "Transaction", "Finance", "#/ap/payment-run"),
            ("AS01", "Create asset", "Master Data", "Assets", "#/assets/create"),
            ("AS02", "Change asset", "Master Data", "Assets", "#/assets/change"),
            ("AS03", "Display asset", "Master Data", "Assets", "#/assets/display"),
            ("AFAB", "Depreciation run", "Transaction", "Assets", "#/assets/depreciation-run"),
            ("KS01", "Create cost centre", "Master Data", "Controlling", "#/co/cost-center/create"),
            ("KS02", "Change cost centre", "Master Data", "Controlling", "#/co/cost-center/change"),
            ("KS03", "Display cost centre", "Master Data", "Controlling", "#/co/cost-center/display"),
            ("KE51", "Create profit centre", "Master Data", "Controlling", "#/co/profit-center/create"),
            ("KO01", "Create internal order", "Master Data", "Controlling", "#/co/internal-order/create"),
            ("KO88", "Internal order settlement", "Transaction", "Controlling", "#/co/settlement"),
            ("SE11", "Data dictionary", "Tools", "DataDictionary", "#/se11"),
            ("SE16N", "Table browser", "Tools", "TableBrowser", "#/se16n"),
        ];
}
