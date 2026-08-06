# Phase 3 — Backend, increment 3: park, submit, approve, maker-checker

Status: **delivered and verified.** This closes item 3 of the increment-2 plan in
[the Phase 3 report](README.md#next), and with it the document lifecycle the
Phase 2 schema has modelled since the beginning — `Parked`, `PendingApproval`,
`Approved` and `Rejected` were unreachable statuses until now.

Per §26: completed components, files, database changes, tests, limitations, next.

## What was built

| Component | State |
| --- | --- |
| Workflow module — domain, contracts, application, infrastructure | New bounded module owning schema `wf` |
| Approval rules, matched by company code, document type, amount and currency | Thresholds stack into multi-level approval |
| Park (FV50) | Same derivation as posting, stops before the ledger |
| Submit for approval | Opens a workflow, or posts directly if nothing is configured to approve |
| Approve / reject | Role-checked, value-limited, maker-checker enforced |
| Approver's inbox | What is waiting on the calling user, and why they may not release it |
| Workflow history on a document | Who decided what, when, and with what comment |

### API

```
POST /api/v1/finance/journal-entries/park                        park a document      (FV50)
POST /api/v1/finance/journal-entries/{cc}/{yr}/{n}/submit         submit for approval
POST /api/v1/finance/journal-entries/{cc}/{yr}/{n}/approve        approve the caller's step
POST /api/v1/finance/journal-entries/{cc}/{yr}/{n}/reject         reject, reason mandatory
GET  /api/v1/finance/journal-entries/{cc}/{yr}/{n}/workflow       approval state and history
GET  /api/v1/finance/approvals                                    the caller's inbox
```

## The design, and why

### Park is a flag on the posting command, not a separate command

`PostJournalEntryCommand.Park` sits alongside `Simulate`, and for the same
reason. All three modes run the *identical* derivation: posting keys, account
checks, controlling derivation, tax generation, balancing. A separate park
command would eventually derive a document slightly differently from the one that
later posts — which means the thing that was approved is not the thing that hit
the ledger. That is the failure this design forecloses.

What differs after derivation is small and explicit: the header's status, whether
open items are created, and whether `PostedBy` is set. A parked document has no
`PostedBy`, because a name in that column on a document that is not in the ledger
is a false statement in the audit trail.

### A parked document is invisible to the ledger

It takes a document number and its lines are written, but the trial balance
already filtered on `Status = Posted`, so it contributes nothing, and no open
items exist. Approval is what creates them. This is verified rather than
asserted: the acceptance suite reads the trial balance before parking, after
parking (unchanged) and after approval (moved by exactly the document's value).

### Finance depends on Workflow through Contracts only

`IApprovalService` and its DTOs live in `S4HERP.Workflow.Contracts`, which
references nothing at all. Finance references that assembly and never touches a
`wf` table (blueprint [02](../blueprint/02-module-boundaries.md) §2.2, ADR-02).

The dependency runs Finance → Workflow and never the reverse, which is what makes
it acyclic: Workflow decides *who must approve* and records that they did, and has
no idea what posting means. Deciding what approval releases stays with the module
that owns the object. The same service will serve a payment run or a business
partner change with no Finance-shaped assumptions to unpick.

### Approval steps name a role, never a person

A step assigned to `alice` is a stuck workflow the day Alice leaves. Steps carry
`ApproverRoleCode`, and the decision is open to anyone holding that role. This is
the one place role membership is itself a business fact, so it needed a new
method on `IAuthorizationEnforcer` — `RoleCodesAsync`. Everywhere else, code asks
for an authorisation object rather than a role.

### The approval limit had to be zero-padded

`W_APPROVE` carries an `AMOUNT_TO` field and the enforcer compares interval
bounds with `string.CompareOrdinal`, because an authorisation field value is text
— most of them, like company code and document type, are not numbers at all.
Ordinal comparison of unpadded numbers is wrong in the dangerous direction and
the harmless one at once: `"6000"` sorts *above* `"50000"`, so a 6,000 document
would be refused by an approver limited to 50,000, and other pairs would pass
what should be refused.

`Workflow.Contracts.AmountLimit` encodes amounts at fixed width —
`000000000000000.0000`, exactly the range of the `decimal(19,4)` every amount is
stored as — so text order is numeric order. The two-level approval check in the
acceptance suite is a 6,000 document against a 50,000 limit, chosen precisely
because it fails without the padding.

### The maker-checker test needed a role that violates segregation of duties

The seed's `FI_APPROVER` holds `F_BKPF_BUK` with activity 03 only — display, not
create — because `SOD003` names posting-plus-approving as a conflict, and a seed
that hands out a combination its own rulebook forbids is not a good example.

But that makes maker-checker unreachable in the seeded configuration, and a
control nobody can trigger is a control nobody has tested. `SegregationOfDutiesRule`
is a *design* control: a rulebook a reviewer reads, not something the enforcer
applies at runtime. Real installations therefore can and eventually will grant a
combination like this.

So the seed also ships `FI_SUPERVISOR` — deliberately in violation of SOD003,
holding both post and approve — and `seed.supervisor` who holds it. The
acceptance suite has that user park a document, submit it, and be refused with
`MAKER_CHECKER_VIOLATION` when they try to approve it. Then somebody else
approves the same document and it posts.

Maker-checker bars **both** the submitter and the document's creator. Barring
only the submitter would let an assistant launder the preparer's document past
the control by submitting it on their behalf.

### No approval rule configured means post, not fail

If no rule matches, `StartAsync` returns `NotRequired` and submission posts the
document. Refusing would strand it: the caller already holds the authority the
plain post endpoint uses, and configuration has said this document needs nobody's
signature. The audit entry says so explicitly.

This is why the seeded rules are per currency. Company code 2000 keeps its books
in THB, and a USD-only rule would silently never match there — every document in
2000 would post unapproved, and nothing would look wrong. THB thresholds are
seeded alongside the USD ones.

## Database changes

Migration `ApprovalWorkflow`, three tables in schema `wf`:

| Table | Notes |
| --- | --- |
| `wf.ApprovalRule` | `CK_ApprovalRule_Amount`; index on (tenant, company code, document type, amount) |
| `wf.WorkflowInstance` | `UX_WorkflowInstance_OnePending` — filtered unique index on `[Status] = 1`, so an object cannot be in two approvals at once |
| `wf.WorkflowStep` | `CK_WorkflowStep_Decided` — a decided step must record who decided it and when |

Row-level security needed no change: `db/scripts/020-row-level-security.sql`
discovers every table with a `TenantId` column and rebuilds the policy on each
start, and `wf` was already in its schema list.

`audit.AuditAction` gains `Park = 13` and `Submit = 14`.

## Two bugs found by running it

### Rolling the migration back was impossible, and had been for every migration

CI has a step asserting that migrations reverse cleanly, and it passes. Against a
real database, this one did not:

```
Cannot DROP TABLE 'wf.ApprovalRule' because it is being referenced
by object 'TenantIsolationPolicy'.
```

`sec.TenantIsolationPolicy` is created `WITH SCHEMABINDING`, so SQL Server
refuses to drop any table it covers — and it covers every table with a
`TenantId` column, which is every table a migration is ever likely to drop. This
is not specific to the Workflow module; it has been true since Phase 2.

**CI missed it because CI is not testing the real schema.** The reversal step
applies migrations to a scratch database and unwinds them *without* running the
post-migration scripts, so no security policy exists to get in the way. A green
reversal test that only passes because the database is missing half of itself is
worse than no test.

The fix is in the migration's `Down`: drop the policy before dropping the tables.
That is safe because the policy is derived state rather than schema —
`db/scripts/020-row-level-security.sql` rebuilds it from `sys.tables` on every
start and on every `--migrate`. Any future migration that drops a tenant-scoped
table needs the same two lines. Verified by rolling this migration back and
reapplying it against the live database.

### An empty migration

The first `ef migrations add` produced an empty migration. The list of
assemblies carrying entity configurations existed in *three* copies — the
composition root, the design-time factory and the architecture tests — and the
Workflow module was added to only the first. EF then diffed a model that had the
tables against a snapshot built from a model that did not, found nothing, and
succeeded.

That is a bad failure: it is silent, and the migration it writes is valid. The
fix is `ModuleRegistration.ModuleAssemblies`, one list that all three call. The
architecture tests now build their model from the host's list rather than a
parallel one, which is the point — a model the tests build differently from the
one the application builds proves nothing about the application.

## Verification

`db/tests/posting-engine.sh` — **43 new checks**, taking that suite from 34 to
**77/77**. Each asserts a specific HTTP status *and* application error code.

```
== Park, submit, approve ==
  PASS  A document parks without posting                       201
  PASS  ...and reports status Parked, posted=false
  PASS  A parked document is absent from the trial balance
  PASS  A parked document cannot be reversed                   422 DOCUMENT_NOT_POSTED
  PASS  A parked document cannot be approved                   422 DOCUMENT_NOT_PENDING_APPROVAL
  PASS  Submission opens an approval                           200
  PASS  ...one step, pending, document awaiting approval
  PASS  A document cannot be submitted twice                   422 DOCUMENT_NOT_PARKED
  PASS  Someone without the approver role is refused           403 NOT_AUTHORIZED
  PASS  The approver's inbox lists the document                200
  PASS  ...and names the waiting document
  PASS  A clerk's inbox is empty                               200
  PASS  ...because approval is by role, not by seniority
  PASS  Approval posts the document                            200
  PASS  ...workflow Approved, document Posted
  PASS  ...and it now moves the trial balance
  PASS  An approved document cannot be approved again          422 DOCUMENT_NOT_PENDING_APPROVAL
  PASS  The workflow history survives the posting              200
  PASS  ...naming who approved it
== Maker-checker ==
  PASS  A user who may both post and approve parks a document  201
  PASS  ...and submits it                                      200
  PASS  ...but cannot approve their own document               422 MAKER_CHECKER_VIOLATION
  PASS  Somebody else can                                      200
  PASS  ...and the document posts
== Approval thresholds ==
  PASS  A larger document parks                                201
  PASS  ...and needs two approvals                             200
  PASS  ...FI_APPROVER then FI_SENIOR_APPROVER
  PASS  The first approval leaves it pending                   200
  PASS  ...still PendingApproval after step 1
  PASS  The first approver cannot also take step 2             403 NOT_AUTHORIZED
  PASS  The second approver releases it                        200
  PASS  ...and only then does it post
  PASS  A document above the approver's limit parks            201
  PASS  ...and is submitted                                    200
  PASS  ...but exceeds the first approver's value limit        403 NOT_AUTHORIZED
== Rejection ==
  PASS  A document parks and is submitted                      201
  PASS  ...and enters approval                                 200
  PASS  Rejection without a reason is refused                  422 REJECTION_COMMENT_REQUIRED
  PASS  Rejection with a reason is recorded                    200
  PASS  ...and the document reads Rejected
  PASS  A rejected document cannot then be approved            422 DOCUMENT_NOT_PENDING_APPROVAL
  PASS  Trial balance still foots after the whole workflow     200
```

### How it was verified here

`docker build` could not restore NuGet packages in this environment — the
sandbox proxy intercepts TLS and the build container does not carry its CA, so
`dotnet restore` fails with NU1301 exactly as npm already did for the UI stage.
The suites were therefore run against the host started from source with
`./run-host.sh`, which mounts the CA, talking to the Compose database. That
exercises the same code; it does not exercise the image. See
[Phase 5 limitation 2](../phase5/README.md#limitations).

## Seeded users

| User | Role | Can |
| --- | --- | --- |
| `seed.accountant` | FI_ACCOUNTANT | Post and park in 1000, 1100, 2000 |
| `seed.clerk` | FI_CLERK_1000 | Post in 1000 only, document type SA only |
| `seed.auditor` | AUDITOR | Read only, enforced at the user-type level |
| `seed.approver` | FI_APPROVER | Display; approve journal entries up to 50,000 |
| `seed.cfo` | FI_SENIOR_APPROVER | Display; approve journal entries up to 10,000,000 |
| `seed.supervisor` | FI_SUPERVISOR | Post **and** approve — an SOD003 conflict, seeded so maker-checker has something to catch |

## Files

```
src/Modules/Workflow/
    S4HERP.Workflow.Domain/Workflow.cs                 ApprovalRule, WorkflowInstance, WorkflowStep
    S4HERP.Workflow.Contracts/ApprovalContracts.cs     IApprovalService, DTOs, AmountLimit
    S4HERP.Workflow.Application/ApprovalService.cs     rule matching, decisions, maker-checker
    S4HERP.Workflow.Infrastructure/WorkflowConfigurations.cs
src/Modules/Finance/S4HERP.Finance.Application/
    JournalApproval.cs                                 submit/approve/reject, ParkedDocumentPoster
    PostJournalEntry.cs                                Park flag, Status on the result
    PostJournalEntryHandler.cs                         park path
src/Modules/Finance/S4HERP.Finance.Api/FinanceEndpoints.cs
src/BuildingBlocks/S4HERP.BuildingBlocks.Application/PipelineBehaviors.cs   RoleCodesAsync
src/Host/S4HERP.Host/
    Infrastructure/ModuleRegistration.cs               one module-assembly list
    Infrastructure/SampleDataSeeder.Business.cs        approval rules, approver roles and users
    Migrations/*_ApprovalWorkflow.cs
```

## Limitations

1. **A parked document cannot be edited.** `TR_JournalEntryLine_NoUpdate` blocks
   every line update, by design, so correcting a parked document means discarding
   it and parking a fresh one, which consumes a document number.
   [Increment 4](increment-4-lifecycle.md) added the discard endpoint, so this is
   no longer a dead end — but real FV50 keeps parked lines editable, and matching
   that means either exempting pre-posting documents from the trigger or holding
   parked lines in a separate staging table. That is a decision worth taking on
   purpose rather than in passing.
2. ~~**No withdraw.**~~ Added by [increment 4](increment-4-lifecycle.md).
3. **No substitute or deputy approver.** If nobody holding the role is available,
   the document waits. Validity-dated role assignment exists and is the natural
   place to build this on.
4. **No escalation, reminders or notification.** An approver learns about a
   document by looking at the inbox. There is no outbox, no email, no timer.
5. **No parallel approval.** Steps are strictly sequential — step 2 cannot be
   decided before step 1. Four-eyes-in-parallel is a common requirement and is
   not modelled.
6. **Approval rules have no maintenance UI or API.** They are seed data. Editing
   them means a database change, and they are exactly the kind of configuration
   that should be audited when it changes.
7. **Rule matching ignores the document's own currency.** Thresholds are compared
   in the company code's local currency, which is right, but a rule can only be
   configured in a currency some company code actually uses — there is no
   translation of a threshold across currencies.
8. ~~**The front end has no approval screens.**~~ Added by
   [increment 4](increment-4-lifecycle.md).
9. **`SegregationOfDutiesRule` is still not enforced at runtime.** It is a
   rulebook, and this increment deliberately relies on that. A conflicting grant
   should at minimum be reported; nothing reports it today.

## Next

The remaining item from the increment-2 plan is **AR/AP**: invoices, payments,
clearing, dunning and the payment run. Open items now exist on both the direct
and the approved path, which is what clearing needs.

Two smaller pieces would pay for themselves first:

1. **Approval screens in the front end** — an inbox tile and the park/submit
   buttons Phase 4 left with nothing to call. The API shape is settled now.
2. **Delete or re-park a rejected document**, which is limitation 1 above and the
   one place the lifecycle currently dead-ends.
