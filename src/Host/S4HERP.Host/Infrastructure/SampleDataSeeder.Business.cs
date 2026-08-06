using Microsoft.EntityFrameworkCore;
using S4HERP.BusinessPartner.Domain;
using S4HERP.Controlling.Domain;
using S4HERP.Finance.Domain;
using S4HERP.Organization.Domain;
using S4HERP.Security.Domain;
using S4HERP.Workflow.Contracts;
using S4HERP.Workflow.Domain;

namespace S4HERP.Host.Infrastructure;

public partial class SampleDataSeeder
{
    private sealed record CoIds(
        long ControllingAreaId,
        Dictionary<string, long> CostCenters,
        Dictionary<string, long> ProfitCenters,
        Dictionary<string, long> InternalOrders);

    private async Task<CoIds> SeedControllingAsync(
        OrgIds orgs, Calendars calendars, ChartOfAccountsIds coa, CancellationToken ct)
    {
        var usd = await CurrencyId("USD", ct);
        var validFrom = new DateOnly(SeedFiscalYear, 1, 1);

        var area = new ControllingArea
        {
            TenantId = _tenantId, Code = "CA01", Name = "KSS controlling area",
            CurrencyId = usd, ChartOfAccountsId = coa.ChartId,
            FiscalYearVariantId = calendars.FiscalYearVariantId,
            CrossCompanyCodeCostAccounting = true, CreatedBy = "SEED",
        };
        db.Add(area);
        await db.SaveChangesAsync(ct);

        foreach (var companyCodeId in orgs.CompanyCodes.Values)
        {
            db.Add(new ControllingAreaCompanyCode
            {
                TenantId = _tenantId, ControllingAreaId = area.Id, CompanyCodeId = companyCodeId,
                ValidFrom = validFrom, CreatedBy = "SEED",
            });
        }

        var profitCenters = new (string Code, string Name, string Segment)[]
        {
            ("PC1000", "Sugar production", "SUGAR"),
            ("PC2000", "Trading", "TRADE"),
            ("PC9000", "Corporate", "CORP"),
        };

        foreach (var pc in profitCenters)
        {
            db.Add(new ProfitCenter
            {
                TenantId = _tenantId, Code = pc.Code, Name = pc.Name, ControllingAreaId = area.Id,
                SegmentId = orgs.Segments[pc.Segment], ValidFrom = validFrom, CreatedBy = "SEED",
            });
        }

        await db.SaveChangesAsync(ct);

        var pcMap = await db.Set<ProfitCenter>()
            .Where(x => x.TenantId == _tenantId).ToDictionaryAsync(x => x.Code, x => x.Id, ct);

        var costCenters = new (string Code, string Name, CostCenterCategory Cat, string Cc, string Pc)[]
        {
            ("CC101000", "Head office administration", CostCenterCategory.Administration, "1000", "PC9000"),
            ("CC102000", "Sales and marketing", CostCenterCategory.Sales, "1000", "PC2000"),
            ("CC110100", "Kampot mill production", CostCenterCategory.Production, "1100", "PC1000"),
            ("CC110200", "Kampot mill maintenance", CostCenterCategory.Service, "1100", "PC1000"),
            ("CC200100", "Thailand administration", CostCenterCategory.Administration, "2000", "PC9000"),
        };

        foreach (var cc in costCenters)
        {
            db.Add(new CostCenter
            {
                TenantId = _tenantId, Code = cc.Code, Name = cc.Name, Category = cc.Cat,
                ControllingAreaId = area.Id, CompanyCodeId = orgs.CompanyCodes[cc.Cc],
                ProfitCenterId = pcMap[cc.Pc], CurrencyId = usd, ValidFrom = validFrom,
                CreatedBy = "SEED",
            });
        }

        db.Add(new InternalOrderType
        {
            TenantId = _tenantId, Code = "0100", Name = "Investment order",
            Category = InternalOrderCategory.Real, NumberRangeCode = "IO",
            BudgetControlEnabled = true,
            DefaultSettlementReceiver = SettlementReceiverType.Asset, CreatedBy = "SEED",
        });
        await db.SaveChangesAsync(ct);

        var ccMap = await db.Set<CostCenter>()
            .Where(x => x.TenantId == _tenantId).ToDictionaryAsync(x => x.Code, x => x.Id, ct);
        var orderTypeId = await db.Set<InternalOrderType>()
            .Where(x => x.TenantId == _tenantId).Select(x => x.Id).SingleAsync(ct);

        db.Add(new InternalOrder
        {
            TenantId = _tenantId, Code = "IO0000000001", Name = "Mill boiler replacement",
            InternalOrderTypeId = orderTypeId, ControllingAreaId = area.Id,
            CompanyCodeId = orgs.CompanyCodes["1100"], ResponsibleCostCenterId = ccMap["CC110100"],
            ProfitCenterId = pcMap["PC1000"], CurrencyId = usd, Budget = 250_000m,
            WarningTolerancePercent = 90m, BlockingTolerancePercent = 105m, CreatedBy = "SEED",
        });
        await db.SaveChangesAsync(ct);

        var ioMap = await db.Set<InternalOrder>()
            .Where(x => x.TenantId == _tenantId).ToDictionaryAsync(x => x.Code, x => x.Id, ct);

        return new CoIds(area.Id, ccMap, pcMap, ioMap);
    }

    // ------------------------------------------------------ business partners

    private sealed record PartnerIds(long Customer, long Vendor, long DualRole, long Intercompany);

    private async Task<PartnerIds> SeedBusinessPartnersAsync(
        OrgIds orgs, ChartOfAccountsIds coa, CancellationToken ct)
    {
        var usd = await CurrencyId("USD", ct);
        var validFrom = new DateOnly(SeedFiscalYear, 1, 1);

        var groups = new (string Code, string Name, PartnerCategory Cat)[]
        {
            ("0001", "Organisation", PartnerCategory.Organization),
            ("0002", "Person", PartnerCategory.Person),
            ("0003", "Group", PartnerCategory.Group),
        };
        foreach (var g in groups)
        {
            db.Add(new PartnerGroup
            {
                TenantId = _tenantId, Code = g.Code, Name = g.Name, Category = g.Cat,
                NumberRangeCode = "BP", UseSameNumber = true, CreatedBy = "SEED",
            });
        }

        var roles = new (string Code, string Name, string? Prereq, bool Cc, bool Sales, bool Purch, AccountType? Recon)[]
        {
            ("BP_GEN", "General business partner", null, false, false, false, null),
            ("CUST", "Customer", null, false, true, false, null),
            ("FI_CUST", "FI customer", "CUST", true, false, false, AccountType.Customer),
            ("VEND", "Vendor", null, false, false, true, null),
            ("FI_VEND", "FI vendor", "VEND", true, false, false, AccountType.Vendor),
            ("EMPL", "Employee", null, false, false, false, null),
            ("CONT", "Contact person", null, false, false, false, null),
            ("BANK", "Bank", null, false, false, false, null),
            ("ICOM", "Intercompany partner", null, false, false, false, null),
        };
        foreach (var r in roles)
        {
            db.Add(new PartnerRole
            {
                TenantId = _tenantId, Code = r.Code, Name = r.Name,
                PrerequisiteRoleCode = r.Prereq, RequiresCompanyCode = r.Cc,
                RequiresSalesArea = r.Sales, RequiresPurchasingOrganization = r.Purch,
                ReconciliationAccountType = r.Recon, CreatedBy = "SEED",
            });
        }

        await db.SaveChangesAsync(ct);

        var groupMap = await db.Set<PartnerGroup>()
            .Where(x => x.TenantId == _tenantId).ToDictionaryAsync(x => x.Code, x => x.Id, ct);
        var roleMap = await db.Set<PartnerRole>()
            .Where(x => x.TenantId == _tenantId).ToDictionaryAsync(x => x.Code, x => x.Id, ct);

        // Category/role compatibility: a person cannot be a bank, a group cannot
        // be an employee or contact.
        foreach (var (code, id) in roleMap)
        {
            foreach (var category in Enum.GetValues<PartnerCategory>())
            {
                var allowed = code switch
                {
                    "BANK" => category == PartnerCategory.Organization,
                    "EMPL" or "CONT" => category == PartnerCategory.Person,
                    "ICOM" => category == PartnerCategory.Organization,
                    _ => category != PartnerCategory.Group || code == "BP_GEN",
                };
                if (allowed)
                {
                    db.Add(new PartnerRoleCategoryCompatibility
                    {
                        TenantId = _tenantId, PartnerRoleId = id, Category = category,
                        CreatedBy = "SEED",
                    });
                }
            }
        }

        await db.SaveChangesAsync(ct);

        var customer = await CreatePartnerAsync(
            "1000000001", "Angkor Beverages Co Ltd", groupMap["0001"], validFrom, ct);
        var vendor = await CreatePartnerAsync(
            "1000000002", "Mekong Logistics Ltd", groupMap["0001"], validFrom, ct);

        // The point of ADR-10: one identity holding both sides.
        var dualRole = await CreatePartnerAsync(
            "1000000003", "Kampot Cane Growers Association", groupMap["0001"], validFrom, ct);

        var intercompany = await CreatePartnerAsync(
            "1000000004", "KSS Thailand", groupMap["0001"], validFrom, ct);

        var arAccount = coa.Accounts["1200000000"];
        var apAccount = coa.Accounts["2100000000"];
        var cc1000 = orgs.CompanyCodes["1000"];

        await AssignCustomerAsync(customer, cc1000, arAccount, orgs, usd, validFrom, roleMap, ct);
        await AssignVendorAsync(vendor, cc1000, apAccount, orgs, usd, validFrom, roleMap, ct);
        await AssignCustomerAsync(dualRole, cc1000, arAccount, orgs, usd, validFrom, roleMap, ct);
        await AssignVendorAsync(dualRole, cc1000, apAccount, orgs, usd, validFrom, roleMap, ct);

        db.Add(new PartnerRoleAssignment
        {
            TenantId = _tenantId, PartnerId = intercompany, PartnerRoleId = roleMap["ICOM"],
            ValidFrom = validFrom, CreatedBy = "SEED",
        });

        db.Add(new PartnerCreditProfile
        {
            TenantId = _tenantId, PartnerId = customer,
            CreditControlAreaId = orgs.CreditControlAreaId, CreditLimit = 500_000m,
            CurrencyId = usd, RiskClass = "B", CreatedBy = "SEED",
        });

        await db.SaveChangesAsync(ct);
        return new PartnerIds(customer, vendor, dualRole, intercompany);
    }

    private async Task<long> CreatePartnerAsync(
        string number, string name, long groupId, DateOnly validFrom, CancellationToken ct)
    {
        var partner = new Partner
        {
            TenantId = _tenantId, PartnerNumber = number, Category = PartnerCategory.Organization,
            PartnerGroupId = groupId, Name = name,
            NormalizedName = name.ToUpperInvariant().Replace(" ", string.Empty),
            SearchTerm1 = name.Split(' ')[0].ToUpperInvariant(), Language = "en",
            CountryOfOrigin = "KH", ValidFrom = validFrom, CreatedBy = "SEED",
        };
        db.Add(partner);
        await db.SaveChangesAsync(ct);

        var address = new PartnerAddress
        {
            TenantId = _tenantId, PartnerId = partner.Id, AddressType = AddressType.Registered,
            IsDefault = true, Line1 = "1 Norodom Boulevard", City = "Phnom Penh",
            CountryCode = "KH", ValidFrom = validFrom, CreatedBy = "SEED",
        };
        db.Add(address);
        await db.SaveChangesAsync(ct);

        db.Add(new PartnerCommunication
        {
            TenantId = _tenantId, PartnerAddressId = address.Id,
            CommunicationType = CommunicationType.Email, IsDefault = true,
            Value = $"accounts@{partner.SearchTerm1!.ToLowerInvariant()}.example", CreatedBy = "SEED",
        });
        db.Add(new PartnerTaxNumber
        {
            TenantId = _tenantId, PartnerId = partner.Id, CountryCode = "KH",
            TaxNumberType = "VAT", TaxNumber = $"KH{number}", CreatedBy = "SEED",
        });
        await db.SaveChangesAsync(ct);

        return partner.Id;
    }

    /// <summary>
    /// Mirrors what the Phase 3 role synchroniser will do transactionally: the
    /// role assignment and its required facet rows are created together.
    /// </summary>
    private async Task AssignCustomerAsync(
        long partnerId, long companyCodeId, long reconciliationAccountId, OrgIds orgs,
        long currencyId, DateOnly validFrom, Dictionary<string, long> roleMap, CancellationToken ct)
    {
        db.Add(new PartnerRoleAssignment
        {
            TenantId = _tenantId, PartnerId = partnerId, PartnerRoleId = roleMap["CUST"],
            ValidFrom = validFrom, CreatedBy = "SEED",
        });
        db.Add(new PartnerRoleAssignment
        {
            TenantId = _tenantId, PartnerId = partnerId, PartnerRoleId = roleMap["FI_CUST"],
            ValidFrom = validFrom, CreatedBy = "SEED",
        });

        var facet = await GetOrCreateCompanyCodeFacetAsync(
            partnerId, companyCodeId, reconciliationAccountId, ct);
        var number = await PartnerNumberAsync(partnerId, ct);

        db.Add(new PartnerCustomer
        {
            TenantId = _tenantId, PartnerCompanyCodeId = facet,
            CustomerAccountNumber = number, AccountGroup = "0001", CreatedBy = "SEED",
        });
        db.Add(new PartnerSalesArea
        {
            TenantId = _tenantId, PartnerId = partnerId,
            SalesOrganizationId = orgs.SalesOrganizationId, DistributionChannel = "10",
            Division = "00", CurrencyId = currencyId, Incoterms = "EXW", CreatedBy = "SEED",
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task AssignVendorAsync(
        long partnerId, long companyCodeId, long reconciliationAccountId, OrgIds orgs,
        long currencyId, DateOnly validFrom, Dictionary<string, long> roleMap, CancellationToken ct)
    {
        db.Add(new PartnerRoleAssignment
        {
            TenantId = _tenantId, PartnerId = partnerId, PartnerRoleId = roleMap["VEND"],
            ValidFrom = validFrom, CreatedBy = "SEED",
        });
        db.Add(new PartnerRoleAssignment
        {
            TenantId = _tenantId, PartnerId = partnerId, PartnerRoleId = roleMap["FI_VEND"],
            ValidFrom = validFrom, CreatedBy = "SEED",
        });

        // The dual-role partner already has a company-code facet from the customer
        // side; the vendor facet attaches to the same row rather than duplicating it.
        var facet = await GetOrCreateCompanyCodeFacetAsync(
            partnerId, companyCodeId, reconciliationAccountId, ct);
        var number = await PartnerNumberAsync(partnerId, ct);

        db.Add(new PartnerVendor
        {
            TenantId = _tenantId, PartnerCompanyCodeId = facet,
            VendorAccountNumber = number, AccountGroup = "0001", CreatedBy = "SEED",
        });
        db.Add(new PartnerPurchasingOrganization
        {
            TenantId = _tenantId, PartnerId = partnerId,
            PurchasingOrganizationId = orgs.PurchasingOrganizationId, PurchasingGroup = "001",
            OrderCurrencyId = currencyId, Incoterms = "DAP", PaymentTerms = "N030",
            CreatedBy = "SEED",
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task<long> GetOrCreateCompanyCodeFacetAsync(
        long partnerId, long companyCodeId, long reconciliationAccountId, CancellationToken ct)
    {
        var existing = await db.Set<PartnerCompanyCode>()
            .Where(x => x.PartnerId == partnerId && x.CompanyCodeId == companyCodeId)
            .Select(x => (long?)x.Id).SingleOrDefaultAsync(ct);

        if (existing is { } id)
        {
            return id;
        }

        var facet = new PartnerCompanyCode
        {
            TenantId = _tenantId, PartnerId = partnerId, CompanyCodeId = companyCodeId,
            ReconciliationAccountId = reconciliationAccountId, PaymentTerms = "N030",
            CreatedBy = "SEED",
        };
        db.Add(facet);
        await db.SaveChangesAsync(ct);
        return facet.Id;
    }

    private Task<string> PartnerNumberAsync(long partnerId, CancellationToken ct) =>
        db.Set<Partner>().Where(x => x.Id == partnerId)
            .Select(x => x.PartnerNumber).SingleAsync(ct);

    // -------------------------------------------------------------- security

    private async Task SeedSecurityAsync(OrgIds orgs, CancellationToken ct)
    {
        var validFrom = new DateOnly(SeedFiscalYear, 1, 1);

        var objects = new (string Code, string Name, string Module, string[] Fields)[]
        {
            ("F_BKPF_BUK", "Accounting document: company code", "Finance", ["BUKRS", "ACTVT"]),
            ("F_BKPF_BLA", "Accounting document: document type", "Finance", ["BLART", "ACTVT"]),
            ("F_BKPF_AMT", "Accounting document: value limit", "Finance", ["BUKRS", "CURR", "AMOUNT_TO"]),
            ("F_BP_GEN", "Business partner maintenance", "BusinessPartner", ["BP_ROLE", "ACTVT"]),
            ("F_BP_BANK", "Business partner bank details", "BusinessPartner", ["ACTVT"]),
            ("K_CSKS", "Cost centre", "Controlling", ["KOSTL", "ACTVT"]),
            ("K_PCA", "Profit centre", "Controlling", ["PRCTR", "ACTVT"]),
            ("S_TABU_DIS", "Table browser: table group", "TableBrowser", ["DICBERCLS", "ACTVT"]),
            ("S_TABU_FLD", "Table browser: field group", "TableBrowser", ["FIELDGRP", "ACTVT"]),
            ("S_TCODE", "Transaction code", "Security", ["TCD"]),
            ("S_EXPORT", "Data export", "Security", ["SCOPE", "MAXROWS"]),
            ("W_APPROVE", "Workflow approval", "Workflow", ["WFTYPE", "AMOUNT_TO"]),
        };

        foreach (var o in objects)
        {
            var authObject = new AuthorizationObject
            {
                TenantId = _tenantId, Code = o.Code, Name = o.Name, Module = o.Module,
                CreatedBy = "SEED",
            };
            db.Add(authObject);
            await db.SaveChangesAsync(ct);

            for (var i = 0; i < o.Fields.Length; i++)
            {
                db.Add(new AuthorizationField
                {
                    TenantId = _tenantId, AuthorizationObjectId = authObject.Id,
                    Code = o.Fields[i], Name = o.Fields[i], DisplayOrder = i, CreatedBy = "SEED",
                });
            }
        }

        var sodRules = new (string Code, string Name, string Rationale, SodSeverity Sev, string A, string B)[]
        {
            ("SOD001", "Vendor bank details and payment run",
                "Redirecting payments to an attacker-controlled account is the mechanism of invoice fraud.",
                SodSeverity.Critical, "F_BP_BANK", "F_BKPF_BUK"),
            ("SOD002", "Business partner creation and vendor invoice posting",
                "Enables fictitious vendor fraud.", SodSeverity.High, "F_BP_GEN", "F_BKPF_BLA"),
            ("SOD003", "Journal posting and journal approval",
                "Defeats maker-checker on financial documents.", SodSeverity.High,
                "F_BKPF_BUK", "W_APPROVE"),
        };

        foreach (var r in sodRules)
        {
            db.Add(new SegregationOfDutiesRule
            {
                TenantId = _tenantId, Code = r.Code, Name = r.Name, Rationale = r.Rationale,
                Severity = r.Sev, ConflictingAuthorizationObjectA = r.A,
                ConflictingAuthorizationObjectB = r.B, CreatedBy = "SEED",
            });
        }

        var roles = new (string Code, string Name)[]
        {
            ("FI_ACCOUNTANT", "Financial accountant"),
            ("FI_CLERK_1000", "Financial clerk, company code 1000"),
            ("FI_APPROVER", "Financial approver"),
            ("FI_SENIOR_APPROVER", "Financial approver, second level"),
            ("FI_SUPERVISOR", "Financial supervisor (posts and approves — violates SOD003)"),
            ("BP_MAINTAINER", "Business partner maintainer"),
            ("AUDITOR", "Auditor (display only)"),
            ("ADMIN", "System administrator"),
        };
        foreach (var r in roles)
        {
            db.Add(new Role { TenantId = _tenantId, Code = r.Code, Name = r.Name, CreatedBy = "SEED" });
        }

        await db.SaveChangesAsync(ct);

        var roleMap = await db.Set<Role>()
            .Where(x => x.TenantId == _tenantId).ToDictionaryAsync(x => x.Code, x => x.Id, ct);

        var objectMap = await db.Set<AuthorizationObject>()
            .Where(x => x.TenantId == _tenantId).ToDictionaryAsync(x => x.Code, x => x.Id, ct);
        var fieldMap = await db.Set<AuthorizationField>()
            .Where(x => x.TenantId == _tenantId)
            .ToDictionaryAsync(x => x.AuthorizationObjectId + "|" + x.Code, x => x.Id, ct);

        // FI_ACCOUNTANT may post in every company code. FI_CLERK_1000 is deliberately
        // narrower, so the authorisation refusal path has something to refuse.
        await GrantAsync(roleMap["FI_ACCOUNTANT"], "F_BKPF_BUK",
            [("BUKRS", "1000", null), ("BUKRS", "1100", null), ("BUKRS", "2000", null),
             ("ACTVT", "01", null), ("ACTVT", "02", null), ("ACTVT", "03", null)],
            objectMap, fieldMap, ct);
        await GrantAsync(roleMap["FI_ACCOUNTANT"], "F_BKPF_BLA",
            [("BLART", "*", null), ("ACTVT", "01", null), ("ACTVT", "03", null)],
            objectMap, fieldMap, ct);

        await GrantAsync(roleMap["FI_CLERK_1000"], "F_BKPF_BUK",
            [("BUKRS", "1000", null), ("ACTVT", "01", null), ("ACTVT", "03", null)],
            objectMap, fieldMap, ct);
        await GrantAsync(roleMap["FI_CLERK_1000"], "F_BKPF_BLA",
            [("BLART", "SA", null), ("ACTVT", "01", null), ("ACTVT", "03", null)],
            objectMap, fieldMap, ct);

        await GrantAsync(roleMap["AUDITOR"], "F_BKPF_BUK",
            [("BUKRS", "*", null), ("ACTVT", "03", null)], objectMap, fieldMap, ct);

        // Approvers get display, never create. SOD003 names exactly this pair —
        // holding both F_BKPF_BUK/01 and W_APPROVE defeats maker-checker — so the
        // seed must not hand out a combination its own rulebook forbids.
        foreach (var role in new[] { "FI_APPROVER", "FI_SENIOR_APPROVER" })
        {
            await GrantAsync(roleMap[role], "F_BKPF_BUK",
                [("BUKRS", "*", null), ("ACTVT", "03", null)], objectMap, fieldMap, ct);
        }

        // Approval limits. Zero-padded fixed width because the enforcer compares
        // interval bounds as text — see Workflow.Contracts.AmountLimit.
        await GrantAsync(roleMap["FI_APPROVER"], "W_APPROVE",
            [("WFTYPE", "JournalEntry", null),
             ("AMOUNT_TO", AmountLimit.Encode(0), AmountLimit.Encode(50_000m))],
            objectMap, fieldMap, ct);
        await GrantAsync(roleMap["FI_SENIOR_APPROVER"], "W_APPROVE",
            [("WFTYPE", "JournalEntry", null),
             ("AMOUNT_TO", AmountLimit.Encode(0), AmountLimit.Encode(10_000_000m))],
            objectMap, fieldMap, ct);

        // Deliberately in violation of SOD003: this role both creates and approves
        // accounting documents. It exists so that maker-checker has something to
        // catch. Segregation of duties is a *design* control — SegregationOfDutiesRule
        // is a rulebook a reviewer reads, not something the enforcer applies — so a
        // real installation can and eventually will grant a combination like this.
        // Maker-checker is the runtime control that stops it becoming a self-approval,
        // and a control nobody in the seed can trigger is a control nobody has tested.
        await GrantAsync(roleMap["FI_SUPERVISOR"], "F_BKPF_BUK",
            [("BUKRS", "*", null), ("ACTVT", "01", null), ("ACTVT", "03", null)],
            objectMap, fieldMap, ct);
        await GrantAsync(roleMap["FI_SUPERVISOR"], "F_BKPF_BLA",
            [("BLART", "*", null), ("ACTVT", "01", null), ("ACTVT", "03", null)],
            objectMap, fieldMap, ct);
        await GrantAsync(roleMap["FI_SUPERVISOR"], "W_APPROVE",
            [("WFTYPE", "JournalEntry", null),
             ("AMOUNT_TO", AmountLimit.Encode(0), AmountLimit.Encode(50_000m))],
            objectMap, fieldMap, ct);

        // Passwords are absent by design: Phase 3 identifies callers by header in
        // Development only, and a seeded credential would outlive the seed.
        await CreateUserAsync("seed.accountant", "Seed Accountant", roleMap["FI_ACCOUNTANT"],
            orgs.CompanyCodes.Values, orgs.CompanyCodes["1000"], validFrom, ct);
        await CreateUserAsync("seed.clerk", "Seed Clerk (company code 1000 only)",
            roleMap["FI_CLERK_1000"], [orgs.CompanyCodes["1000"]],
            orgs.CompanyCodes["1000"], validFrom, ct);
        await CreateUserAsync("seed.auditor", "Seed Auditor", roleMap["AUDITOR"],
            orgs.CompanyCodes.Values, orgs.CompanyCodes["1000"], validFrom, ct,
            UserType.Auditor);
        await CreateUserAsync("seed.approver", "Seed Approver (up to 50,000)",
            roleMap["FI_APPROVER"], orgs.CompanyCodes.Values, orgs.CompanyCodes["1000"],
            validFrom, ct);
        await CreateUserAsync("seed.cfo", "Seed CFO (second-level approver)",
            roleMap["FI_SENIOR_APPROVER"], orgs.CompanyCodes.Values, orgs.CompanyCodes["1000"],
            validFrom, ct);
        await CreateUserAsync("seed.supervisor", "Seed Supervisor (posts and approves)",
            roleMap["FI_SUPERVISOR"], orgs.CompanyCodes.Values, orgs.CompanyCodes["1000"],
            validFrom, ct);

        await SeedApprovalRulesAsync(validFrom, ct);
    }

    /// <summary>
    /// Two levels of approval on accounting documents. Thresholds are per currency
    /// because a threshold without one is meaningless: company code 2000 keeps its
    /// books in THB, so a USD rule would silently never match there and every
    /// document would post unapproved.
    /// </summary>
    private async Task SeedApprovalRulesAsync(DateOnly validFrom, CancellationToken ct)
    {
        var thresholds = new (string Currency, decimal Level1, decimal Level2)[]
        {
            ("USD", 0m, 5_000m),
            // Roughly the USD figures at 36 THB, rounded to something a human would
            // actually configure.
            ("THB", 0m, 180_000m),
        };

        foreach (var (currencyCode, level1, level2) in thresholds)
        {
            var currencyId = await CurrencyId(currencyCode, ct);

            db.Add(new ApprovalRule
            {
                TenantId = _tenantId,
                Code = $"JE_{currencyCode}_L1",
                Name = $"Journal entry, first approval ({currencyCode})",
                DocumentTypeCode = null,
                FromAmount = level1,
                CurrencyId = currencyId,
                ApproverRoleCode = "FI_APPROVER",
                StepSequence = 10,
                MakerCheckerEnforced = true,
                ValidFrom = validFrom,
                CreatedBy = "SEED",
            });

            db.Add(new ApprovalRule
            {
                TenantId = _tenantId,
                Code = $"JE_{currencyCode}_L2",
                Name = $"Journal entry, second approval above {level2:N0} {currencyCode}",
                DocumentTypeCode = null,
                FromAmount = level2,
                CurrencyId = currencyId,
                ApproverRoleCode = "FI_SENIOR_APPROVER",
                StepSequence = 20,
                MakerCheckerEnforced = true,
                ValidFrom = validFrom,
                CreatedBy = "SEED",
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task CreateUserAsync(
        string userName, string displayName, long roleId, IEnumerable<long> companyCodeIds,
        long defaultCompanyCodeId, DateOnly validFrom, CancellationToken ct,
        UserType userType = UserType.Service)
    {
        var user = new User
        {
            TenantId = _tenantId, UserName = userName, DisplayName = displayName,
            Email = $"{userName}@example.invalid", UserType = userType,
            // Truthful about how these accounts authenticate: in Development the
            // request header is the identity provider. No credential is seeded.
            ExternalIdentityProvider = "development-header",
            ExternalSubjectId = userName,
            Language = "en", TimeZone = "Asia/Phnom_Penh",
            DefaultCompanyCodeId = defaultCompanyCodeId, ValidFrom = validFrom,
            CreatedBy = "SEED",
        };
        db.Add(user);
        await db.SaveChangesAsync(ct);

        db.Add(new UserRole
        {
            TenantId = _tenantId, UserId = user.Id, RoleId = roleId,
            ValidFrom = validFrom, CreatedBy = "SEED",
        });
        foreach (var companyCodeId in companyCodeIds)
        {
            db.Add(new UserCompanyCode
            {
                TenantId = _tenantId, UserId = user.Id, CompanyCodeId = companyCodeId,
                CreatedBy = "SEED",
            });
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// One authorisation grant. All values belong to the same grant, which is what
    /// lets the enforcer refuse to combine fields across separate authorisations.
    /// </summary>
    private async Task GrantAsync(
        long roleId, string objectCode,
        (string Field, string From, string? To)[] values,
        Dictionary<string, long> objectMap, Dictionary<string, long> fieldMap,
        CancellationToken ct)
    {
        var objectId = objectMap[objectCode];
        var grant = new RoleAuthorization
        {
            TenantId = _tenantId, RoleId = roleId, AuthorizationObjectId = objectId,
            CreatedBy = "SEED",
        };
        db.Add(grant);
        await db.SaveChangesAsync(ct);

        foreach (var (field, fromValue, toValue) in values)
        {
            db.Add(new RoleAuthorizationValue
            {
                TenantId = _tenantId, RoleAuthorizationId = grant.Id,
                AuthorizationFieldId = fieldMap[objectId + "|" + field],
                FromValue = fromValue, ToValue = toValue, IsWildcard = fromValue == "*",
                CreatedBy = "SEED",
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
