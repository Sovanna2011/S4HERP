# Increment 7: approval on the payment run

Status: **delivered and verified.** Named as next by
[increment 6](increment-6-payment-run.md#next), limitation 2.

Per §26: completed components, files, database changes, tests, limitations, next.

## What was built

| Component | State |
| --- | --- |
| `ApprovalRule.ObjectType` | A rule now says what it governs |
| `IApprovalService.IsApprovalRequiredAsync` | Ask the matcher without starting anything |
| Submit / approve / reject on a run | Reuses the increment 3 engine unchanged |
| A release threshold in the seed | Above it a run waits; below it, it does not |
| `PaymentRunStatus` | Gains `PendingApproval`, `Approved`, `Rejected` |
| Execution gate | An unsubmitted run over the threshold is refused |

### API

```
POST /api/v1/finance/payment-runs/{runId}/submit    open the approval
POST /api/v1/finance/payment-runs/{runId}/approve   release it for execution
POST /api/v1/finance/payment-runs/{runId}/reject    reject it, with a reason
```

## The design, and why

### The Workflow module learned nothing about payments

`ApprovalService` still does not know what a payment run is. It matches rules,
records decisions and enforces maker-checker on an *object type and an id*, and
Finance decides what an approval releases. Finance → Workflow stays one-way; the
only new thing crossing the boundary is a string.

That is what made this increment small. The engine written in increment 3 for
journal entries needed one field to serve a second object type, and the field is
about rules, not about payments.

### A rule that does not say what it governs governs everything

`ApprovalRule.ObjectType` is the one schema change, and it exists because of a
bug that would otherwise have shipped silently. Before this increment there was
one object type, so a rule matching on company code, document type and amount
matched correctly by construction. Add payment runs and the seeded rule *journal
entry, first approval (USD), from 0.00* starts governing payment runs too — not
because anyone configured that, but because the matcher had no way to ask.

The migration therefore backfills existing rows to `'JournalEntry'` rather than
leaving them NULL. NULL means "any object type", which is a reasonable thing for
somebody to write deliberately and the wrong reading of a rule written when
journal entries were the only possibility. An upgrade must not change what an
existing control does.

The acceptance suite asserts the narrowing rather than trusting it: the journal
approval inbox is checked to **not** list a payment run. A check that a filter
excludes something is worth more than a check that it includes something,
because the including version passes whether or not the filter works.

### Execute asks the same question submit does

The gap that would make all of this decorative: propose a run, never submit it,
press execute. `ExecutePaymentRunHandler` closes it by calling
`IsApprovalRequiredAsync` — the same matcher `StartAsync` uses, deliberately not
a second copy of the rule logic — and refusing with `PAYMENT_RUN_NEEDS_APPROVAL`
when a rule matches.

`IsApprovalRequiredAsync` and `StartAsync` share `MatchRulesAsync`. Two
implementations of "does this need approval" is one implementation and one
future security advisory.

### Approval releases; it does not pay

`Approved` is a separate status from `Executed`. An approval that posted the
payments immediately would make approving and paying the same act performed by
the approver, which is the segregation this control exists to create. Somebody
still has to execute the run, and the audit log has both entries with both names.

### A release threshold, not a blanket rule

The seeded payment-run rule was first written with `FromAmount = 0` — every run,
however small, waits for a second person. That is the wrong default and the
suite said so immediately: the routine 300 USD run from increment 6 stopped
executing.

It is wrong for a reason worth stating. Approval on every payment, including the
daily small ones, is how approval becomes a rubber stamp: the approver who clicks
forty times a day is not reading the fortieth. The seed now sets a release
threshold — 1,000 USD, 36,000 THB — and both paths are tested: a run under it
answers `NotRequired` to submission and executes unapproved, a run over it waits.

Thresholds are per currency for the same reason the journal thresholds are
(increment 3): company code 2000 keeps its books in THB, and a USD-only rule
would silently never match there.

### Maker-checker, tested against a maker who could otherwise approve

The first version of this check had the accountant submit the run and then try to
approve it, expecting `MAKER_CHECKER_VIOLATION`. It got `NOT_AUTHORIZED`: the
accountant holds no `W_APPROVE` at all, so the pipeline turned them away before
maker-checker was ever consulted. The check passed for the wrong reason and
proved nothing — the same false positive found in the journal workflow in
[increment 4](increment-4-lifecycle.md#two-bugs-found-by-running-it).

The run is now proposed and submitted by `seed.supervisor`, the seeded role that
deliberately violates SOD003 by both preparing and approving, and whose
`W_APPROVE` grant was extended to `WFTYPE = PaymentRun` for exactly this reason.
The refusal is now maker-checker refusing, which is the thing under test.

`ApprovalService.DecideAsync` checks maker-checker *before* the approver-role
check, so the person who both made the document and holds the approver role gets
the accurate message rather than "you are not an approver".

## Database changes

Migration `ApprovalRuleObjectType`: one nullable `nvarchar(60)` column on
`wf.ApprovalRule`, the rule-matching index rebuilt to lead with it, and the
backfill described above.

No `sec.TenantIsolationPolicy` drop, unlike the migrations that remove
tenant-scoped tables. The policy is SCHEMABINDING against `TenantId`; adding or
dropping a column it does not reference is permitted.

Rolled down and back up against the seeded database to check it: both directions
apply cleanly. The round trip does relabel existing payment-run rules to
`JournalEntry`, because `Down` drops the column and the re-applied backfill has
nothing left to distinguish them — noted on the migration itself.

The backfill sets `IsCrossTenant` in session context around the `UPDATE` and
restores the prior value afterwards. Without it the RLS predicate — which denies
when session context is unset — would match zero rows and the migration would
report success having done nothing. A silent no-op is the one failure mode a data
migration must not have.

## Verification

Four suites, **234 checks**, all passing against the container image.

| Suite | Checks | Was |
| --- | --- | --- |
| Posting engine (`db/tests/posting-engine.sh`) | 188 | 162 |
| Browser (`ui5/test/ui-acceptance.mjs`) | 27 | 27 |
| Database integrity (`db/tests/integrity-rules.sql`) | 14 | 14 |
| Architecture (`tests/S4HERP.ArchitectureTests`) | 5 | 5 |

```
== Payment run: execution ==
  PASS  Submitting a run below the release threshold asks nobody   200
  PASS  ...it stays Proposed and reports NotRequired
  PASS  A run below the release threshold executes unapproved      200
  ...
== Payment run approval ==
  PASS  A second vendor invoice posts                              201
  PASS  A proposal is created                                      201
  PASS  An unapproved run cannot be executed                       422 PAYMENT_RUN_NEEDS_APPROVAL
  PASS  Deciding before submission is refused                      422 PAYMENT_RUN_NOT_AWAITING_APPROVAL
  PASS  Submitting opens an approval                               200
  PASS  ...the run is now PendingApproval
  PASS  ...and it still cannot be executed                         422 PAYMENT_RUN_NOT_PROPOSED
  PASS  The maker cannot approve their own run                     422 MAKER_CHECKER_VIOLATION
  PASS  Someone without the approver role is refused               403 NOT_AUTHORIZED
  PASS  The run appears in the approver's inbox                    200
  PASS  ...no: the journal inbox is scoped to journal entries
  PASS  The approver releases the run                              200
  PASS  ...to Approved, not paid
  PASS  An approved run executes                                   200
  PASS  ...posting the payments
== Payment run rejection ==
  PASS  A third vendor invoice posts                               201
  PASS  It is proposed and submitted                               201
  PASS  ...submitted                                               200
  PASS  Rejection without a reason is refused                      422 REJECTION_COMMENT_REQUIRED
  PASS  Rejection with a reason is recorded                        200
  PASS  ...and the run reads Rejected
  PASS  A rejected run cannot be executed                          422 PAYMENT_RUN_NOT_PROPOSED
  PASS  Trial balance still foots after approved and rejected runs 200
  PASS  ...difference 0.0000
```

### Two false-red defects fixed in the harness

Both were found the same way: a run came back red and the code turned out to be
fine. A test suite that cries wolf costs exactly as much as one that misses a
bug, because the next real failure gets the same shrug.

**Overlapping runs.** Several checks measure a trial-balance delta around their
own postings, so two suites against one database corrupt each other. Observed
once as a 133/136 with no product cause. `posting-engine.sh` now takes an
exclusive `flock` and refuses to start rather than report a failure it invented.

**The container reported healthy before it was usable.** The Docker `HEALTHCHECK`
probed `/health/live`, which is deliberately green from the moment the process
listens — while migrations and the sample seed are still running. A suite started
on that signal gets `403 NOT_AUTHORIZED` on every request, because no role has
been granted yet. This increment's first full run reported **172 failures** for
that reason and not one of them was real.

That is not only a test problem. `depends_on: service_healthy` and every "wait
for healthy, then call it" script believe the same signal, so the container was
lying to anything that asked. Three changes:

- `HealthProbe` now probes `/health/ready`, which is 503 until
  `MigrationState.Completed` is set — after seeding, not before.
- The Dockerfile's `--start-period` goes from 20s to 180s, since the window now
  has to cover first-run setup. Failures inside the start period do not mark a
  container unhealthy, so this costs only how long a genuinely broken container
  takes to be called broken.
- `posting-engine.sh` waits for `/health/ready` and gives up after five minutes
  with one sentence, rather than emitting 172 meaningless reds.

## Files

```
src/Modules/Workflow/S4HERP.Workflow.Domain/Workflow.cs                     ApprovalRule.ObjectType
src/Modules/Workflow/S4HERP.Workflow.Contracts/ApprovalContracts.cs         IsApprovalRequiredAsync
src/Modules/Workflow/S4HERP.Workflow.Application/ApprovalService.cs         matcher narrowed by object type
src/Modules/Workflow/S4HERP.Workflow.Infrastructure/WorkflowConfigurations.cs
src/Modules/Finance/S4HERP.Finance.Domain/Banking.cs                        three new run statuses
src/Modules/Finance/S4HERP.Finance.Application/PaymentRun.cs                submit, approve, reject, execution gate
src/Modules/Finance/S4HERP.Finance.Api/FinanceEndpoints.cs
src/Host/S4HERP.Host/Infrastructure/ModuleRegistration.cs                   one class, three commands
src/Host/S4HERP.Host/Infrastructure/SampleDataSeeder.Business.cs            release threshold, W_APPROVE grants
src/Host/S4HERP.Host/Migrations/*_ApprovalRuleObjectType.cs
src/Host/S4HERP.Host/HealthProbe.cs                                         probe readiness, not liveness
src/Host/S4HERP.Host/Dockerfile                                             start period covers first-run setup
db/tests/posting-engine.sh                                                  approval and rejection blocks, readiness gate, flock guard
```

## Limitations

1. **No approval inbox for payment runs.** `GET /finance/approvals` is scoped to
   journal entries — proven by a test, but the consequence is that an approver
   has no way to *find* a run awaiting them; they have to be sent the run id. A
   generic inbox over `wf.WorkflowInstance` is the fix and it is not written.
2. **No second level.** Only `PR_{currency}_L1` is seeded, so every run over the
   threshold takes exactly one signature regardless of size. The engine supports
   steps — the journal rules use two — so this is seed configuration, not code.
3. **No re-approval after the proposal changes.** A run approved today and
   executed next week pays whatever is selected *now*. The items are frozen at
   proposal time, so in practice the amounts cannot drift, but nothing checks
   that the approved total still matches at execution. It should.
4. **Rejected is terminal.** A rejected run cannot be corrected and resubmitted;
   a new proposal has to be created. That is defensible but it is not what SAP
   does, and it loses the connection between the two.
5. **Still no payment file** (increment 6, limitation 1), **no cash discount**,
   **no partner bank details**, **no dunning**, **no front end**. Approval made
   the run safer to execute; it did not make it reach a bank.

## Next

**The payment file** — SEPA / ISO 20022 pain.001 — is now the largest gap in
AR/AP: everything up to the bank exists and nothing leaves the building. After
that, dunning, then a generic approval inbox (limitation 1) which would serve
both object types and is small.

Outside AR/AP the untouched modules are unchanged: Asset Accounting, Controlling
allocations and settlement, the SE11 data dictionary, the SE16N table browser,
the custom-object framework and the integration outbox.
