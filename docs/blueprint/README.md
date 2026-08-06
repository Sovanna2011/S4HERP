# S4HERP — Phase 1: Solution Blueprint

Status: **Approved and in build.** Phase 2 (Database) is delivered — see
[docs/phase2](../phase2/README.md) for what was built and verified, and
[docs/adr/016-018](../adr/016-018-phase2-decisions.md) for the three decisions
taken during it plus one correction to this blueprint (the formatted document
number must carry the company code).

This document remains the reviewed baseline; later deltas are recorded as ADRs
rather than edited in, so the two stay distinguishable.

This blueprint covers the twelve areas named in the starting instruction:
architecture, module boundaries, enterprise structure, BP synchronisation, the
universal journal, SE11 metadata, SE16N authorisation and query design, the
custom-object framework, the user security model, project structure, database
schema strategy, and the roadmap.

## Documents

| # | Document | Covers |
| --- | --- | --- |
| 01 | [Solution Architecture](01-solution-architecture.md) | Layers, CQRS, dispatcher, cross-cutting concerns, deployment topology, verified stack |
| 02 | [Module Boundaries](02-module-boundaries.md) | Bounded contexts, dependency rules, the module contract |
| 03 | [Enterprise Structure](03-enterprise-structure.md) | Org objects, assignment rules, validity, compatibility guards |
| 04 | [Business Partner](04-business-partner.md) | BP model, roles, ERD, synchronisation design |
| 05 | [Universal Journal & Posting](05-universal-journal-and-posting.md) | Journal model, currency architecture, posting engine, document numbering |
| 06 | [Business Processes](06-business-processes.md) | R2R, O2C, P2P, asset lifecycle, allocations, intercompany |
| 07 | [Data Dictionary (SE11)](07-data-dictionary-se11.md) | Metadata model, activation pipeline, drift control |
| 08 | [Table Browser (SE16N)](08-table-browser-se16n.md) | Authorisation model, query pipeline, hard controls |
| 09 | [Custom Objects](09-custom-objects.md) | Z/Y/ZZ framework, lifecycle, field placement policy |
| 10 | [Security Model](10-security-model.md) | Users, authorisation objects, org levels, maker-checker, SoD |
| 11 | [T-Code Framework](11-tcode-framework.md) | Alias registry, routing, authorisation binding, global search |
| 12 | [Database Strategy](12-database-strategy.md) | Schemas, conventions, tenancy, concurrency, partitioning, migrations |
| 13 | [Project Structure](13-project-structure.md) | Repository and solution layout, naming, assembly boundaries |
| 14 | [Roadmap](14-roadmap.md) | Milestones, sequencing, risks, definition of done |

## Executive summary

S4HERP is a **modular monolith**: one ASP.NET Core 10 deployable containing
thirteen bounded modules over one SQL Server 2025 database with a
schema-per-domain layout, fronted by OpenUI5 applications sharing a
Fiori-Launchpad-style shell.

The decision that drives most of the others is that **a financial posting must
commit atomically**. A single journal entry writes the header, its lines, open
items, controlling postings, asset postings, workflow state, and the audit
record. The specification requires this in §22.1, and it is the correct
requirement — a ledger that can half-post is not a ledger. That rules out
splitting Finance, Controlling, and Assets into separately deployed services
with compensating sagas, so module boundaries are enforced in-process by
assembly references and architecture tests rather than by network hops.

The second structural decision is that the **universal journal is the single
source of accounting truth**. `fin.JournalEntryLine` carries every reporting
dimension and every required currency amount on each line. Accounts Receivable,
Accounts Payable, Asset Accounting and Controlling are projections of it, not
parallel ledgers that must later be reconciled to it. Reconciliation reports
therefore verify a projection against its own source rather than comparing two
independently maintained truths.

## Key decisions

Each is argued in the linked document. Decisions marked **⚠** need a business
answer before Phase 2 starts; they are collected under *Open questions* below.

| ID | Decision | Where |
| --- | --- | --- |
| ADR-01 | Modular monolith, one deployable; not microservices | [01](01-solution-architecture.md#adr-01) |
| ADR-02 | One database, schema per domain, no cross-schema writes | [12](12-database-strategy.md#adr-02) |
| ADR-03 | ⚠ Shared database multi-tenancy, `TenantId` everywhere, EF global filters **plus** SQL Server Row-Level Security | [12](12-database-strategy.md#adr-03) |
| ADR-04 | CQRS in-process; no event sourcing | [01](01-solution-architecture.md#adr-04) |
| ADR-05 | No MediatR — licence is RPL-1.5 or commercial. In-house dispatcher instead | [01](01-solution-architecture.md#adr-05) |
| ADR-06 | ⚠ OData V4 has no stable .NET 10 release; scope OData to read-only list/report feeds and use REST for commands | [01](01-solution-architecture.md#adr-06) |
| ADR-07 | Universal journal as one wide insert-only line table, partitioned by fiscal year | [05](05-universal-journal-and-posting.md#adr-07) |
| ADR-08 | Currency amounts as named columns, not amount/currency rows | [05](05-universal-journal-and-posting.md#adr-08) |
| ADR-09 | Clearing state lives in `fin.OpenItem`, never mutates a posted journal line | [05](05-universal-journal-and-posting.md#adr-09) |
| ADR-10 | BP is the only identity; customer and vendor are role facets, synchronised in the same transaction | [04](04-business-partner.md#adr-10) |
| ADR-11 | ⚠ Document numbers are gapless per (tenant, company code, year, type), which serialises allocation | [05](05-universal-journal-and-posting.md#adr-11) |
| ADR-12 | Custom fields become real typed columns; JSON only for non-reportable extras | [09](09-custom-objects.md#adr-12) |
| ADR-13 | No DDL from the browser — SE11 activation emits a migration into a pull request | [07](07-data-dictionary-se11.md#adr-13) |
| ADR-14 | SE16N runs on a physically read-only SQL login | [08](08-table-browser-se16n.md#adr-14) |
| ADR-15 | ⚠ OpenUI5 pinned to a maintenance line; smart controls and `sap.viz` are **not** in OpenUI5 | [01](01-solution-architecture.md#adr-15) |
| ADR-16 | One `DbContext`, not one per module | [adr](../adr/016-018-phase2-decisions.md#adr-16) |
| ADR-17 | `Organization.Domain` is the shared kernel | [adr](../adr/016-018-phase2-decisions.md#adr-17) |
| ADR-18 | No foreign keys on journal dimension columns | [adr](../adr/016-018-phase2-decisions.md#adr-18) |

## Verified stack findings

These were checked against the live package feeds while writing the blueprint,
not recalled. They change what the specification assumed.

| Component | Specified | Verified reality | Consequence |
| --- | --- | --- | --- |
| `Microsoft.AspNetCore.OData` | "OData V4 preferred binding protocol" | Latest **stable is 9.5.0, targeting `net8.0`**. `10.0.0` is at `preview.2` | ADR-06. OData is scoped, not the backbone |
| `MediatR` | Implied by "CQRS" | 14.2.0 is **RPL-1.5 or a paid commercial licence** (Lucky Penny Software). RPL-1.5 is reciprocal — it would require releasing S4HERP source | ADR-05. Do not take the dependency |
| `FluentValidation` | "Use FluentValidation" | Core is **12.1.1**. `FluentValidation.AspNetCore` (11.3.1) is the **deprecated** auto-validation shim | Use core 12.x with an explicit endpoint filter |
| OpenUI5 | "SAPUI5/OpenUI5" | Newest is **1.150.0**; maintenance lines 1.120.x / 1.136.x. `sap.ui.comp` smart controls and `sap.viz` ship in **SAPUI5 only** | ADR-15 — needs a licence answer |
| `Serilog.AspNetCore` | Specified | 10.0.0 — aligned with .NET 10 | Adopt |
| `Hangfire.AspNetCore` / `Quartz` | Either | 1.8.24 / 3.19.1, both current | Hangfire recommended, see [01](01-solution-architecture.md) |
| .NET / SQL Server | ASP.NET Core 10, SQL Server 2025 | .NET 10.0.10 and SQL Server 2025 RTM-CU7 (17.0.4065.4) both running in this repo today | Confirmed |

## Open questions

Blocking Phase 2. Each one changes the schema or the delivery plan, so they are
worth answering before any table is created.

1. **SAPUI5 licence (ADR-15).** Do you hold an SAP licence that permits
   `sap.ui.comp` (smart filter bars, smart tables) and `sap.viz`? OpenUI5 is
   Apache-2.0 but ships neither. If the answer is OpenUI5-only, the SE16N
   browser, list reports and analytical tiles need a different control strategy,
   and that affects Phase 4 sizing significantly.
2. **Tenancy (ADR-03).** Shared database for all tenants, or a database per
   tenant for larger customers? Shared is the default here; the alternative is
   supportable with the same schema but changes connection routing, migration
   orchestration, and the backup story.
3. **Gapless numbering (ADR-11).** Which jurisdictions must the first companies
   satisfy? Legally gapless VAT document numbering forces serialised allocation
   per document type and caps concurrent posting throughput. If gaps are
   tolerable anywhere, those document types can use a much faster path.
4. **Parallel ledgers.** Which accounting standards are live at go-live — IFRS
   plus one local GAAP, or more? The ledger dimension is in the journal from day
   one either way, but valuation, depreciation areas and financial statement
   versions multiply per ledger.
5. **Khmer collation and fonts.** Khmer text in `NVARCHAR` is safe, but sort
   order needs an explicit collation choice, and Khmer numerals and date formats
   need a decision for reports and printed documents.
6. ~~**Existing scaffold.**~~ **Resolved.** The `Products` sample is deleted; the
   container stack carried forward.

## What Phase 1 deliberately does not contain

No entity classes, no DDL, no migrations, no controllers, no UI5 views. Where
this blueprint shows a table shape or a field list it is specifying an interface
for review, not staging code. Phase 2 turns the agreed shapes into DDL and EF
configurations; the [roadmap](14-roadmap.md) sets out the sequence.
