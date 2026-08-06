# ADR-016 … ADR-018 — Decisions taken during Phase 2

Three decisions that the Phase 1 blueprint did not settle, plus one correction
to it. Recorded here rather than edited into the blueprint, so the blueprint
stays the reviewed artefact and the deltas stay visible.

---

<a id="adr-16"></a>
## ADR-16 — One `DbContext`, not one per module

**Status:** accepted.

**Context.** The blueprint fixes module boundaries but does not say how EF Core
is arranged behind them. The modular-monolith convention is a `DbContext` per
module with its own migration history.

**Decision.** A single `S4herpDbContext` in `BuildingBlocks.Infrastructure`.
Each module supplies `IEntityTypeConfiguration<T>` types, and the host registers
the module assemblies in its composition root. Boundaries are kept by schema
ownership, not by context ownership.

**Why.** ADR-01 requires that a financial posting commits journal header, lines,
open items, controlling entries, asset entries, workflow state and audit in one
transaction. With one context per module that means either sharing a connection
and transaction between contexts — which works but makes every posting a manual
enlistment exercise — or accepting `TransactionScope` and its escalation
behaviour. One context makes the atomic posting the default rather than
something each call site has to get right.

**Cost.** The context knows every entity type, so the model is large and a
module cannot be removed without touching the host's registration list. Both are
acceptable; the registration list is one file and is the composition root's job.

---

<a id="adr-17"></a>
## ADR-17 — `Organization.Domain` is the shared kernel

**Status:** accepted. Amends the architecture-test rule in blueprint
[02](../blueprint/02-module-boundaries.md).

**Context.** The blueprint says a module may reference only another module's
`Contracts` assembly. Every module needs `CompanyCode`, `Currency`,
`FiscalYearVariant` and the rest of the enterprise structure — as entity types,
for foreign keys and navigation, not as DTOs.

**Decision.** `Organization.Domain` is an explicit shared kernel. Other modules
reference it directly. The architecture-test rule becomes: *no module references
another module's `Domain` assembly except `Organization.Domain`.*

**Why.** Enterprise structure is not one module's private concept; it is the
coordinate system the whole ledger is expressed in. Duplicating `CompanyCode`
into a contracts DTO for foreign-key purposes would mean either losing the
constraint or maintaining a parallel type that must never diverge. A shared
kernel is the honest name for what this already is.

**Boundary preserved.** `Organization` still depends on nothing. Constraints that
point *out* of `org` into another module's schema are declared by the owning
module instead — `CompanyCode → ChartOfAccounts` and
`BusinessPartnerCompanyCode → GLAccount` are both configured from
`Finance.Infrastructure`, because Finance owns `fin.GLAccount` and Finance is
allowed to depend on Organization and BusinessPartner. The dependency graph stays
acyclic.

---

<a id="adr-18"></a>
## ADR-18 — No foreign keys on `fin.JournalEntryLine` dimensions

**Status:** accepted.

**Context.** A journal line carries roughly fifteen dimension columns — cost
centre, profit centre, internal order, segment, functional area, business area,
plant, branch, partner company code, partner profit centre and so on. Declaring
a foreign key on each means fifteen index seeks per inserted line, on the table
that receives more inserts than every other table combined.

**Decision.** `fin.JournalEntryLine` declares one foreign key: to its header,
for cascade of pre-posting drafts. Dimension columns carry none. Validity is
enforced by the posting engine at derivation time, and verified after the fact
by a reconciliation job.

The header keeps its foreign keys — company code, document type, currency,
exchange rate type — because it is one row per document rather than several, and
because those four are what make a document interpretable at all.

**Why this is safe here and not in general.** Dropping referential integrity is
usually a bad trade. It is defensible in this one place because nothing writes to
the journal except the posting engine (an architecture test asserts it), the
posting engine validates every dimension before insert, and the rows are
immutable afterwards — so there is no later edit that could invalidate a
reference. The usual failure mode of missing foreign keys, "some other code path
wrote a bad value", is closed off by ADR-07 rather than by hope.

**Verification instead of constraints.** A Phase 3 reconciliation job checks for
orphaned dimension references and reports them. That converts a hard per-insert
cost into a cheap periodic scan.

---

<a id="correction-1"></a>
## Correction to the blueprint — the formatted document number

**Blueprint said:** document numbers format as `KSS-2026-SA-0000000123`
(blueprint [05](../blueprint/05-universal-journal-and-posting.md), §14 of the
specification).

**That is wrong,** and seeding the intercompany pair proved it: company codes
1000 and 2000 each drew document number 700000001 from their own number ranges —
correct behaviour — and produced the identical formatted string, colliding on
what had been a tenant-wide unique index.

**Corrected to** `KSS-1000-2026-IC-0700000001`: the company code is part of the
formatted number, and the unique index is
`(TenantId, CompanyCodeId, DocumentNumberFormatted)`.

A document number is only ever unique within a company code and fiscal year —
that is why SAP requires all three to open a document. A "unique" formatted
identifier that omits the company code is not an identifier.
