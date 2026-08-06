# Phase 2 — Entity Relationship Diagrams

Generated from the foreign keys actually present in the migrated database, so
these show what was built rather than what was intended.

Cross-schema references are drawn in the diagram of the *referencing* schema.
Where two tables are joined by more than one foreign key — a journal header
referencing `Currency` twice, say — the pair is drawn once with the count noted.


## Enterprise structure (org, cfg)

`org.Tenant` is the only table without a `TenantId`: it defines them. Company code freezes its currency, chart of accounts and fiscal year variant once a document is posted — see blueprint 03, §3.4.

```mermaid
erDiagram
    cfg_Currency ||--o{ cfg_ExchangeRate : "restrict x2"
    cfg_ExchangeRateType ||--o{ cfg_ExchangeRate : "restrict"
    cfg_FiscalYearVariant ||--o{ cfg_FiscalYearPeriod : "cascade"
    org_CompanyCode ||--o{ cfg_NumberRange : "restrict"
    cfg_NumberRange ||--o{ cfg_NumberRangeGap : "cascade"
    cfg_PostingPeriodVariant ||--o{ cfg_PostingPeriod : "cascade"
    org_CompanyCode ||--o{ org_Branch : "restrict"
    cfg_Currency ||--o{ org_Company : "restrict"
    cfg_Currency ||--o{ org_CompanyCode : "restrict"
    cfg_FiscalYearVariant ||--o{ org_CompanyCode : "restrict"
    cfg_PostingPeriodVariant ||--o{ org_CompanyCode : "restrict"
    fin_ChartOfAccounts ||--o{ org_CompanyCode : "restrict"
    org_Company ||--o{ org_CompanyCode : "restrict"
    org_CreditControlArea ||--o{ org_CompanyCode : "restrict"
    cfg_Currency ||--o{ org_CreditControlArea : "restrict"
    org_CompanyCode ||--o{ org_Plant : "restrict"
    org_CompanyCode ||--o{ org_PurchasingOrganization : "restrict"
    cfg_Currency ||--o{ org_SalesOrganization : "restrict"
    org_CompanyCode ||--o{ org_SalesOrganization : "restrict"
```

## Finance — general ledger and the universal journal (fin)

`JournalEntryLine` shows exactly one foreign key, to its header. The fifteen dimension columns carry none, by ADR-18. `OpenItem` points back at the line so clearing never touches the posted journal (ADR-09).

```mermaid
erDiagram
    cfg_Currency ||--o{ fin_ClearingHeader : "restrict"
    org_CompanyCode ||--o{ fin_ClearingHeader : "restrict"
    fin_ClearingHeader ||--o{ fin_ClearingLine : "cascade"
    fin_OpenItem ||--o{ fin_ClearingLine : "restrict"
    fin_FinancialStatementItem ||--o{ fin_FinancialStatementItem : "restrict"
    fin_FinancialStatementVersion ||--o{ fin_FinancialStatementItem : "cascade"
    fin_ChartOfAccounts ||--o{ fin_FinancialStatementVersion : "restrict"
    fin_ChartOfAccounts ||--o{ fin_GLAccount : "restrict"
    fin_GLAccountGroup ||--o{ fin_GLAccount : "restrict"
    cfg_Currency ||--o{ fin_GLAccountCompanyCode : "restrict"
    fin_GLAccount ||--o{ fin_GLAccountCompanyCode : "cascade"
    org_CompanyCode ||--o{ fin_GLAccountCompanyCode : "restrict"
    fin_ChartOfAccounts ||--o{ fin_GLAccountGroup : "restrict"
    cfg_Currency ||--o{ fin_JournalEntryHeader : "restrict"
    cfg_DocumentType ||--o{ fin_JournalEntryHeader : "restrict"
    cfg_ExchangeRateType ||--o{ fin_JournalEntryHeader : "restrict"
    org_CompanyCode ||--o{ fin_JournalEntryHeader : "restrict"
    fin_JournalEntryHeader ||--o{ fin_JournalEntryLine : "cascade"
    fin_JournalEntryLine ||--o{ fin_OpenItem : "restrict"
```

## Controlling (co)

Cost centres, profit centres and internal orders all key on a controlling area (rule R6), and all carry validity dates (rule R8).

```mermaid
erDiagram
    co_ControllingArea ||--o{ co_AllocationCycle : "restrict"
    co_AllocationCycle ||--o{ co_AllocationCycleSegment : "cascade"
    co_CostCenter ||--o{ co_AllocationCycleSegment : "restrict x2"
    co_InternalOrder ||--o{ co_AllocationCycleSegment : "restrict"
    co_StatisticalKeyFigure ||--o{ co_AllocationCycleSegment : "restrict"
    cfg_Currency ||--o{ co_ControllingArea : "restrict"
    cfg_FiscalYearVariant ||--o{ co_ControllingArea : "restrict"
    co_ControllingArea ||--o{ co_ControllingAreaCompanyCode : "cascade"
    org_CompanyCode ||--o{ co_ControllingAreaCompanyCode : "restrict"
    cfg_Currency ||--o{ co_CostCenter : "restrict"
    co_ControllingArea ||--o{ co_CostCenter : "restrict"
    co_CostCenter ||--o{ co_CostCenter : "restrict"
    co_ProfitCenter ||--o{ co_CostCenter : "restrict"
    org_CompanyCode ||--o{ co_CostCenter : "restrict"
    org_FunctionalArea ||--o{ co_CostCenter : "restrict"
    cfg_Currency ||--o{ co_InternalOrder : "restrict"
    co_ControllingArea ||--o{ co_InternalOrder : "restrict"
    co_CostCenter ||--o{ co_InternalOrder : "restrict"
    co_InternalOrderType ||--o{ co_InternalOrder : "restrict"
    co_ProfitCenter ||--o{ co_InternalOrder : "restrict"
    org_CompanyCode ||--o{ co_InternalOrder : "restrict"
    co_InternalOrder ||--o{ co_InternalOrderSettlementRule : "cascade"
    co_ControllingArea ||--o{ co_ProfitCenter : "restrict"
    co_ProfitCenter ||--o{ co_ProfitCenter : "restrict"
    org_Segment ||--o{ co_ProfitCenter : "restrict"
    co_ControllingArea ||--o{ co_StatisticalKeyFigure : "restrict"
```

## Business Partner (mdm)

One `BusinessPartner`, many role facets. `BusinessPartnerCustomer` and `BusinessPartnerVendor` both hang off the same `BusinessPartnerCompanyCode` row — that shared row is what makes a dual-role partner one identity (ADR-10).

```mermaid
erDiagram
    mdm_BusinessPartnerGroup ||--o{ mdm_BusinessPartner : "restrict"
    mdm_BusinessPartner ||--o{ mdm_BusinessPartnerAddress : "cascade"
    mdm_BusinessPartner ||--o{ mdm_BusinessPartnerBank : "cascade"
    mdm_BusinessPartnerAddress ||--o{ mdm_BusinessPartnerCommunication : "cascade"
    fin_GLAccount ||--o{ mdm_BusinessPartnerCompanyCode : "restrict"
    mdm_BusinessPartner ||--o{ mdm_BusinessPartnerCompanyCode : "cascade"
    org_CompanyCode ||--o{ mdm_BusinessPartnerCompanyCode : "restrict"
    cfg_Currency ||--o{ mdm_BusinessPartnerCreditProfile : "restrict"
    mdm_BusinessPartner ||--o{ mdm_BusinessPartnerCreditProfile : "cascade"
    org_CreditControlArea ||--o{ mdm_BusinessPartnerCreditProfile : "restrict"
    mdm_BusinessPartnerCompanyCode ||--o{ mdm_BusinessPartnerCustomer : "cascade"
    mdm_BusinessPartner ||--o{ mdm_BusinessPartnerIdentification : "cascade"
    cfg_Currency ||--o{ mdm_BusinessPartnerPurchasingOrganization : "restrict"
    mdm_BusinessPartner ||--o{ mdm_BusinessPartnerPurchasingOrganization : "cascade"
    org_PurchasingOrganization ||--o{ mdm_BusinessPartnerPurchasingOrganization : "restrict"
    mdm_BusinessPartner ||--o{ mdm_BusinessPartnerRelationship : "restrict x2"
    mdm_BusinessPartner ||--o{ mdm_BusinessPartnerRoleAssignment : "cascade"
    mdm_BusinessPartnerRole ||--o{ mdm_BusinessPartnerRoleAssignment : "restrict"
    mdm_BusinessPartnerRole ||--o{ mdm_BusinessPartnerRoleCategory : "cascade"
    cfg_Currency ||--o{ mdm_BusinessPartnerSalesArea : "restrict"
    mdm_BusinessPartner ||--o{ mdm_BusinessPartnerSalesArea : "cascade"
    org_SalesOrganization ||--o{ mdm_BusinessPartnerSalesArea : "restrict"
    mdm_BusinessPartner ||--o{ mdm_BusinessPartnerTaxNumber : "cascade"
    mdm_BusinessPartnerCompanyCode ||--o{ mdm_BusinessPartnerVendor : "cascade"
```

## Security (sec)

`RoleAuthorization` plus `RoleAuthorizationValue` is what lets a role say "post in company code 1000 up to 50,000 USD" rather than merely "may post".

```mermaid
erDiagram
    sec_AuthorizationObject ||--o{ sec_AuthorizationField : "cascade"
    sec_User ||--o{ sec_PasswordHistory : "cascade"
    sec_AuthorizationObject ||--o{ sec_RoleAuthorization : "restrict"
    sec_Role ||--o{ sec_RoleAuthorization : "cascade"
    sec_AuthorizationField ||--o{ sec_RoleAuthorizationValue : "restrict"
    sec_RoleAuthorization ||--o{ sec_RoleAuthorizationValue : "cascade"
    sec_Permission ||--o{ sec_RolePermission : "cascade"
    sec_Role ||--o{ sec_RolePermission : "cascade"
    sec_Role ||--o{ sec_RoleTransactionCode : "cascade"
    sec_User ||--o{ sec_UserCompanyCode : "cascade"
    sec_Role ||--o{ sec_UserRole : "cascade"
    sec_User ||--o{ sec_UserRole : "cascade"
    sec_User ||--o{ sec_UserSession : "cascade"
    sec_User ||--o{ sec_UserSubstitution : "restrict x2"
```

## Audit (audit)

Append-only, enforced by trigger. Not reachable from the table browser at any authorisation level.

```mermaid
erDiagram
    audit_AuditLog ||--o{ audit_AuditLogField : "cascade"
```
