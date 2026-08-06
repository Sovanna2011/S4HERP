# 12 — Database Strategy

Target: **SQL Server 2025** (verified running as 17.0.4065.4 RTM-CU7 in this
repository), accessed through **EF Core 10**.

<a id="adr-02"></a>
## ADR-02 — One database, schema per domain

**Decision.** One database per tenant group, with the ten schemas from §13. A
module owns its schema and no other module writes to it.

**Why.** The atomic-posting requirement ([ADR-01](01-solution-architecture.md#adr-01))
needs one transaction across finance, controlling, assets, workflow and audit.
Multiple databases would mean distributed transactions — MSDTC, or a saga, or
silent inconsistency. Schemas give the ownership boundary and the permission
boundary without giving up the transaction.

| Schema | Owner | Contents |
| --- | --- | --- |
| `org` | Organization | Tenant, company, company code, plant, branch, areas |
| `cfg` | Organization / DataDictionary / Customization | Variants, document types, number ranges, dictionary metadata, T-codes |
| `mdm` | BusinessPartner | Business partner and all facets |
| `fin` | Finance / Assets | Accounts, journal, open items, clearing, invoices, payments, assets |
| `co` | Controlling | Cost centres, profit centres, orders, allocations, settlement |
| `wf` | Workflow | Rules, instances, steps, approvals, delegations |
| `sec` | Security | Users, roles, authorisations, sessions, SoD |
| `rpt` | Reporting | Report catalogue, variants, layouts, materialised summaries |
| `intg` | Integration | Adapters, outbox, inbound staging, API keys, webhooks |
| `audit` | Audit | Business audit history, change documents, browser log |
| `z*` | Customization | Custom tables, mirroring the standard schema they extend |

<a id="adr-03"></a>
## ADR-03 — Shared-database tenancy with RLS as a second layer

**Decision.** Every table carries `TenantId`. EF Core global query filters apply
it to all normal access. **SQL Server Row-Level Security** applies it again at
the engine, under a session context set on every connection.

**Why both.** The EF global query filter is the primary mechanism and it is
good — but it is bypassable in exactly the places this system has by design:
raw SQL, `FromSqlRaw`, the table browser's compiled queries, reporting views,
and any `IgnoreQueryFilters()` a developer adds while debugging and forgets to
remove. In a system whose headline features include a generic table browser and
an ad-hoc reporting layer, a filter that lives only in the ORM is not enough.

RLS is set up as:

```
Session context 'TenantId' set on connection open, from the authenticated principal
Security predicate function on TenantId, applied as a FILTER predicate to every
tenanted table, plus a BLOCK predicate on insert/update to prevent cross-tenant writes
```

The cost is a predicate on every query plan and the discipline of setting
session context correctly on every connection, including Hangfire jobs and
migrations. That is a known, testable cost. Cross-tenant data disclosure is
neither.

**Deployment variant.** A large tenant can be given its own database with the
identical schema; only connection routing changes. This is a per-customer
operational decision, not a code path, which is why open question 2 needs
answering before Phase 2 sets up migration orchestration — one database and
fifty are different deployment problems.

## 12.1 Conventions

| Aspect | Convention |
| --- | --- |
| Naming | `PascalCase` tables and columns, singular table names |
| Primary keys | Surrogate `bigint IDENTITY` or `uniqueidentifier`; **business keys get their own unique index** |
| Journal keys | Composite business key `(TenantId, CompanyCodeId, FiscalYear, DocumentNumber, LineNumber)` as the clustered key |
| Foreign keys | Always declared, always indexed |
| Booleans | `bit NOT NULL` with a default |
| Money | `decimal(19,4)` |
| Quantity, rates | `decimal(23,6)` |
| Timestamps | `datetime2(7)`, **UTC**, suffixed `Utc` |
| Text | `nvarchar`; `nvarchar(max)` only for genuine long text |
| Codes | `nvarchar` with the length from the domain, never `max` |
| Concurrency | `rowversion` on every mutable table |
| Soft delete | `IsActive` / validity dates on master and config; **never** on posted documents |
| Audit columns | `CreatedAtUtc`, `CreatedBy`, `ModifiedAtUtc`, `ModifiedBy` on every mutable table |

**Why surrogate plus business key.** The journal needs its natural key
clustered, because that is how documents are read. Everything else is easier to
reference with a stable surrogate — a business partner's number can be
reassigned during migration, and a chart of accounts can be renamed, without
rewriting every foreign key.

## 12.2 Indexing

| Table | Clustered | Key non-clustered |
| --- | --- | --- |
| `fin.JournalEntryLine` | `(TenantId, CompanyCodeId, FiscalYear, DocumentNumber, LineNumber)` | `(TenantId, CompanyCodeId, GLAccountId, PostingDate) INCLUDE (amounts)`; `(TenantId, CompanyCodeId, BusinessPartnerId, PostingDate)`; `(TenantId, CostCenterId, PostingDate)`; `(TenantId, ProfitCenterId, PostingDate)` |
| `fin.OpenItem` | `(TenantId, CompanyCodeId, Id)` | `(TenantId, CompanyCodeId, BusinessPartnerId, ClearingStatus, DueDate)` — the aging and payment-run index |
| `mdm.BusinessPartner` | `Id` | `(TenantId, PartnerNumber)` unique; `(TenantId, SearchTerm1)`; `(TenantId, TaxNumber)` unique per country |
| `audit.AuditLog` | `(TenantId, CreatedAtUtc, Id)` | `(TenantId, ObjectType, ObjectId)` |

**Columnstore for analytics.** Once the journal is large, add a **nonclustered
columnstore index** to `fin.JournalEntryLine` covering the reporting columns.
SQL Server maintains it alongside the rowstore, so transactional document access
keeps the clustered index while trial balances, P&L and segment reporting get
batch-mode scans. This is the standard pattern for an ACDOCA-shaped table and it
is why the wide-table design is affordable at scale.

Add it when measurements justify it, not on day one — it costs insert throughput
and the posting path is latency-sensitive.

## 12.3 Partitioning

`fin.JournalEntryLine`, `fin.JournalEntryHeader` and `audit.AuditLog` are
partitioned by **fiscal year**.

| Benefit | Detail |
| --- | --- |
| Maintenance | Index rebuilds and statistics per partition, not per table |
| Archiving | Switch out a closed year to an archive table as a metadata operation |
| Query elimination | Reports filtered by year touch one partition |
| Loading | Year-end carry-forward writes into a fresh partition |

Partitioning is set up in Phase 2, not retrofitted. Adding partitioning to a
populated multi-hundred-million-row table later means a full rebuild during a
maintenance window, and the window grows every year the decision is deferred.

## 12.4 Transactions

Per §22.1:

- One transaction per financial business transaction, opened by the pipeline's
  transaction behaviour, spanning every write the command makes.
- Default isolation `READ COMMITTED` with **`READ_COMMITTED_SNAPSHOT ON`**, so
  readers do not block writers. That matters here because reporting and the
  table browser run continuously against the same tables the posting engine
  writes.
- `SERIALIZABLE` only where genuinely required — number range allocation uses an
  explicit `UPDLOCK, ROWLOCK` on a single row instead, which is narrower.
- No database calls inside loops, and no external HTTP inside a transaction —
  hence the outbox.
- Long-running work (payment runs, depreciation, allocations) is chunked into
  many short transactions with restartable checkpoints, not one long one.

## 12.5 Concurrency

| Record class | Mechanism |
| --- | --- |
| Master data, configuration, workflow | `rowversion` sent to the client, required on update, HTTP 409 with a `ProblemDetails` describing the conflict on mismatch |
| Pre-posting documents | Same |
| Posted documents | None — immutable, so there is nothing to conflict |
| Number ranges | Pessimistic row lock, held for the minimum span |
| Balance-affecting aggregates | Not stored; derived. There is no balance row to contend on |

On a 409 the client is offered reload, compare and retry. Silent
last-write-wins is prohibited by §22.1 and is the source of the "my change
disappeared" class of support ticket.

## 12.6 Migrations

- EF Core migrations, one per change, reviewed in a pull request.
- Both `Up` **and** `Down` executed in CI against a real SQL Server container —
  the same image this repository already runs.
- Idempotent SQL scripts generated for environments where a DBA applies changes
  by hand.
- Data migrations separated from schema migrations, and made restartable.
- Seed data (§24) applied by a versioned seeder that is safe to re-run.
- Migrations run at deployment, not at application start, for production. The
  development stack in this repository migrates on startup, which is right for
  development and wrong for a cluster where three replicas would race —
  Phase 5 splits these paths.

## 12.7 Performance and integrity

| Concern | Approach |
| --- | --- |
| Reporting load | Read-only login, optionally a read replica; heavy reports materialised into `rpt.*` by background jobs with a stated staleness |
| Pagination | Mandatory server-side, allow-listed sort fields, primary key appended for stability, page size capped |
| Caching | Configuration and master data cached with explicit invalidation on change. **Never** cache authorisation decisions or balances |
| Business logic in SQL | Prohibited in triggers per §22, with two exceptions: RLS predicates, and the insert-only guard on posted tables |
| Constraints | Checks, uniques and foreign keys declared in the database, not only in the application — the browser and any DBA activity must hit the same rules |
| Backup | Full plus log backups for point-in-time recovery; restore tested on a schedule, because an untested backup is a hypothesis |
| Archiving | Partition switch by fiscal year into archive tables, still queryable by reporting |
