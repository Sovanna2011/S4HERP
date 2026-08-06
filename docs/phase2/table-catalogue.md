# Phase 2 — Table Catalogue

Generated from the live database after migration, not hand-maintained, so it
cannot drift from what was actually created.

**83 tables · 296 indexes · 107 foreign keys · 29 check constraints · 5 integrity
triggers**, across 7 of the 10 schemas. `wf`, `rpt` and `intg` exist but hold no
tables yet — Workflow, Reporting and Integration are Phase 3.

Column legend: **Tn** = carries `TenantId` and is covered by row-level security;
**RV** = carries a `rowversion` optimistic concurrency token. Posted financial
documents deliberately have neither an update path nor a concurrency token
(ADR-07).


## `org` — 11 tables

Enterprise structure — the organisational units everything else is posted against.

| Table | Purpose | Cols | Idx | FK | Chk | Tn | RV |
| --- | --- | ---: | ---: | ---: | ---: | :-: | :-: |
| `Branch` | Location below a company code | 12 | 3 | 1 | 0 | ✓ | ✓ |
| `BusinessArea` | Cross-company internal reporting dimension | 10 | 2 | 0 | 0 | ✓ | ✓ |
| `Company` | Consolidation unit grouping company codes | 12 | 3 | 1 | 0 | ✓ | ✓ |
| `CompanyCode` | Legal entity. The primary posting dimension | 24 | 8 | 6 | 0 | ✓ | ✓ |
| `CreditControlArea` | Credit limit boundary, spanning company codes | 11 | 3 | 1 | 0 | ✓ | ✓ |
| `FunctionalArea` | Cost-of-sales reporting dimension | 10 | 2 | 0 | 0 | ✓ | ✓ |
| `Plant` | Operational unit below a company code | 12 | 3 | 1 | 0 | ✓ | ✓ |
| `PurchasingOrganization` | Purchasing unit; home of the BP purchasing facet | 11 | 3 | 1 | 0 | ✓ | ✓ |
| `SalesOrganization` | Sales unit; home of the BP sales-area facet | 12 | 4 | 2 | 0 | ✓ | ✓ |
| `Segment` | IFRS 8 reporting segment, derivable from the profit centre | 10 | 2 | 0 | 0 | ✓ | ✓ |
| `Tenant` | Isolation boundary. Not tenant-scoped — defines the tenants everything else refers to | 10 | 2 | 0 | 0 | · | ✓ |

## `cfg` — 13 tables

Configuration — calendars, currencies, document control, tax and T-codes.

| Table | Purpose | Cols | Idx | FK | Chk | Tn | RV |
| --- | --- | ---: | ---: | ---: | ---: | :-: | :-: |
| `Currency` | ISO 4217 currency with display decimals | 11 | 2 | 0 | 0 | ✓ | ✓ |
| `DocumentType` | Document class, its number range and permitted account types | 16 | 2 | 0 | 0 | ✓ | ✓ |
| `ExchangeRate` | Validity-dated rate between two currencies | 14 | 5 | 3 | 1 | ✓ | ✓ |
| `ExchangeRateType` | Rate type (M, B, G) with quotation direction | 11 | 2 | 0 | 0 | ✓ | ✓ |
| `FiscalYearPeriod` | Date boundaries of each period of a fiscal year | 13 | 4 | 1 | 1 | ✓ | ✓ |
| `FiscalYearVariant` | Period count, special periods, calendar alignment | 13 | 2 | 0 | 0 | ✓ | ✓ |
| `NumberRange` | Concurrency-safe document number source (ADR-11) | 18 | 3 | 1 | 1 | ✓ | ✓ |
| `NumberRangeGap` | Allocated but uncommitted numbers, for audit | 11 | 3 | 1 | 0 | ✓ | ✓ |
| `PostingKey` | Account type plus debit/credit semantics of a line | 13 | 2 | 0 | 0 | ✓ | ✓ |
| `PostingPeriod` | Open/closed state per account type, year and period | 13 | 3 | 1 | 0 | ✓ | ✓ |
| `PostingPeriodVariant` | Named set of posting period rules | 9 | 2 | 0 | 0 | ✓ | ✓ |
| `TaxCode` | Validity-dated tax rate, direction and posting account | 17 | 2 | 0 | 1 | ✓ | ✓ |
| `TransactionCode` | T-code alias, route and authorisation binding | 20 | 3 | 0 | 0 | ✓ | ✓ |

## `mdm` — 17 tables

Master data — the Business Partner and all of its role facets.

| Table | Purpose | Cols | Idx | FK | Chk | Tn | RV |
| --- | --- | ---: | ---: | ---: | ---: | :-: | :-: |
| `BusinessPartner` | The single partner identity (ADR-10) | 25 | 5 | 1 | 1 | ✓ | ✓ |
| `BusinessPartnerAddress` | Typed, validity-dated address | 19 | 4 | 1 | 0 | ✓ | ✓ |
| `BusinessPartnerBank` | Bank details. Maker-checker controlled | 18 | 4 | 1 | 0 | ✓ | ✓ |
| `BusinessPartnerCommunication` | Contact channel, attached to an address | 11 | 3 | 1 | 0 | ✓ | ✓ |
| `BusinessPartnerCompanyCode` | Company-code financial facet | 21 | 5 | 3 | 0 | ✓ | ✓ |
| `BusinessPartnerCreditProfile` | Credit limit per credit control area | 14 | 5 | 3 | 1 | ✓ | ✓ |
| `BusinessPartnerCustomer` | Customer facet of the company-code data | 13 | 3 | 1 | 0 | ✓ | ✓ |
| `BusinessPartnerGroup` | Account group driving number range and field status | 12 | 2 | 0 | 0 | ✓ | ✓ |
| `BusinessPartnerIdentification` | Registration and identity documents | 14 | 3 | 1 | 0 | ✓ | ✓ |
| `BusinessPartnerPurchasingOrganization` | Purchasing facet | 16 | 5 | 3 | 0 | ✓ | ✓ |
| `BusinessPartnerRelationship` | Validity-dated typed link between partners | 12 | 4 | 2 | 1 | ✓ | ✓ |
| `BusinessPartnerRole` | Role catalogue and its prerequisites | 14 | 2 | 0 | 0 | ✓ | ✓ |
| `BusinessPartnerRoleAssignment` | Validity-dated role held by a partner | 11 | 4 | 2 | 1 | ✓ | ✓ |
| `BusinessPartnerRoleCategory` | Which categories may hold which roles | 9 | 3 | 1 | 0 | ✓ | ✓ |
| `BusinessPartnerSalesArea` | Sales-area facet | 20 | 5 | 3 | 0 | ✓ | ✓ |
| `BusinessPartnerTaxNumber` | Tax number per country and type; duplicate guard | 11 | 3 | 1 | 0 | ✓ | ✓ |
| `BusinessPartnerVendor` | Vendor facet of the company-code data | 13 | 3 | 1 | 0 | ✓ | ✓ |

## `fin` — 13 tables

Finance — chart of accounts, the universal journal, open items and clearing.

| Table | Purpose | Cols | Idx | FK | Chk | Tn | RV |
| --- | --- | ---: | ---: | ---: | ---: | :-: | :-: |
| `ChartOfAccounts` | Account catalogue, shareable across company codes | 12 | 2 | 0 | 0 | ✓ | ✓ |
| `ClearingHeader` | Clearing document | 14 | 4 | 2 | 0 | ✓ | · |
| `ClearingLine` | Open item cleared by a clearing document | 7 | 4 | 2 | 0 | ✓ | · |
| `FinancialStatementItem` | Node of a financial statement version | 14 | 4 | 2 | 0 | ✓ | ✓ |
| `FinancialStatementVersion` | Balance sheet / P&L hierarchy definition | 11 | 3 | 1 | 0 | ✓ | ✓ |
| `GLAccount` | Chart-level account master | 18 | 4 | 2 | 1 | ✓ | ✓ |
| `GLAccountCompanyCode` | Company-code level account settings | 19 | 5 | 3 | 0 | ✓ | ✓ |
| `GLAccountGroup` | Account number range grouping within a chart | 12 | 3 | 1 | 0 | ✓ | ✓ |
| `JournalEntryHeader` | Accounting document header. Immutable once posted | 33 | 9 | 4 | 3 | ✓ | ✓ |
| `JournalEntryLine` | **The universal journal.** Insert-only, partitioned by fiscal year | 60 | 5 | 1 | 4 | ✓ | · |
| `Ledger` | Accounting standard's book; one leading, N non-leading | 13 | 3 | 0 | 0 | ✓ | ✓ |
| `OpenItem` | Mutable open-item state, kept out of the journal (ADR-09) | 25 | 4 | 1 | 2 | ✓ | ✓ |
| `PostingIdempotency` | Deduplicates retried posting requests | 7 | 2 | 0 | 0 | ✓ | · |

## `co` — 10 tables

Controlling — cost and profit centres, internal orders and allocation.

| Table | Purpose | Cols | Idx | FK | Chk | Tn | RV |
| --- | --- | ---: | ---: | ---: | ---: | :-: | :-: |
| `AllocationCycle` | Distribution or assessment cycle | 16 | 3 | 1 | 1 | ✓ | ✓ |
| `AllocationCycleSegment` | Sender, receiver and tracing factor | 16 | 7 | 5 | 2 | ✓ | ✓ |
| `ControllingArea` | Cost accounting boundary | 14 | 4 | 2 | 0 | ✓ | ✓ |
| `ControllingAreaCompanyCode` | Validity-dated company code assignment | 11 | 4 | 2 | 1 | ✓ | ✓ |
| `CostCenter` | Cost collector with hierarchy and validity | 22 | 9 | 6 | 1 | ✓ | ✓ |
| `InternalOrder` | Real or statistical order with budget control | 21 | 9 | 6 | 1 | ✓ | ✓ |
| `InternalOrderSettlementRule` | Settlement receiver and share | 13 | 3 | 1 | 1 | ✓ | ✓ |
| `InternalOrderType` | Order class, category and settlement default | 13 | 2 | 0 | 0 | ✓ | ✓ |
| `ProfitCenter` | Profit centre with hierarchy and segment derivation | 18 | 5 | 3 | 1 | ✓ | ✓ |
| `StatisticalKeyFigure` | Non-financial allocation basis | 12 | 3 | 1 | 0 | ✓ | ✓ |

## `sec` — 16 tables

Security — users, roles, authorisation objects and segregation of duties.

| Table | Purpose | Cols | Idx | FK | Chk | Tn | RV |
| --- | --- | ---: | ---: | ---: | ---: | :-: | :-: |
| `AuthorizationField` | Field of an authorisation object, e.g. BUKRS | 11 | 3 | 1 | 0 | ✓ | ✓ |
| `AuthorizationObject` | Value-carrying authorisation, e.g. F_BKPF_BUK | 10 | 2 | 0 | 0 | ✓ | ✓ |
| `LoginHistory` | Login attempts, successful and failed | 9 | 3 | 0 | 0 | ✓ | · |
| `PasswordHistory` | Previous password hashes for reuse prevention | 5 | 3 | 1 | 0 | ✓ | · |
| `Permission` | Fine-grained action permission | 10 | 2 | 0 | 0 | ✓ | ✓ |
| `Role` | Named bundle of permissions and authorisations | 11 | 2 | 0 | 0 | ✓ | ✓ |
| `RoleAuthorization` | Instance of an authorisation object granted to a role | 9 | 4 | 2 | 0 | ✓ | ✓ |
| `RoleAuthorizationValue` | Permitted value, range or wildcard for a field | 12 | 4 | 2 | 0 | ✓ | ✓ |
| `RolePermission` | Permission granted by a role | 9 | 4 | 2 | 0 | ✓ | ✓ |
| `RoleTransactionCode` | T-codes a role may enter | 9 | 3 | 1 | 0 | ✓ | ✓ |
| `SegregationOfDutiesRule` | Conflicting authorisation pair with severity | 14 | 2 | 0 | 1 | ✓ | ✓ |
| `User` | User master with type, validity and lock state | 27 | 3 | 0 | 1 | ✓ | ✓ |
| `UserCompanyCode` | Company codes a user may act in | 9 | 3 | 1 | 0 | ✓ | ✓ |
| `UserRole` | Validity-dated role grant | 11 | 4 | 2 | 0 | ✓ | ✓ |
| `UserSession` | Active and historical sessions | 8 | 4 | 1 | 0 | ✓ | · |
| `UserSubstitution` | Delegated authority for a date range | 13 | 4 | 2 | 1 | ✓ | ✓ |

## `audit` — 3 tables

Audit — business change history and table-browser access log.

| Table | Purpose | Cols | Idx | FK | Chk | Tn | RV |
| --- | --- | ---: | ---: | ---: | ---: | :-: | :-: |
| `AuditLog` | Business audit history. Append-only | 14 | 5 | 0 | 0 | ✓ | · |
| `AuditLogField` | Field-level before/after values | 6 | 3 | 1 | 0 | ✓ | · |
| `TableBrowserLog` | Every SE16N query, with its filters | 13 | 4 | 0 | 0 | ✓ | · |
