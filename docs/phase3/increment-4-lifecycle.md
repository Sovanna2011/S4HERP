# Increment 4: withdraw, discard, and the approval screens

Status: **delivered and verified.** This closes the two gaps
[increment 3](increment-3-workflow.md#limitations) left open — its limitation 1
(a rejected document was a dead end), limitation 2 (no withdraw), and
limitation 8 (the front end had no approval screens), along with
[Phase 4](../phase4/README.md#limitations) limitation 6.

It spans backend and front end, because half a lifecycle nobody can drive is not
a feature.

Per §26: completed components, files, database changes, tests, limitations, next.

## What was built

| Component | State |
| --- | --- |
| Withdraw | The submitter pulls a document back out of approval, returning it to `Parked` |
| Discard | A document that never reached the ledger is deleted; the number stays spent |
| Park button | On the journal entry screen, beside Simulate and Post |
| Approval actions | Submit · Approve · Reject · Withdraw · Discard on the document, by status |
| Approval history | Steps, decisions, who decided and their comment, on the document |
| Approvals inbox | A launchpad tile and an app listing what is waiting on the caller |
| Khmer translations | Every new string, in both bundles |

### API

```
POST   /api/v1/finance/journal-entries/{cc}/{yr}/{n}/withdraw   pull back out of approval
DELETE /api/v1/finance/journal-entries/{cc}/{yr}/{n}            discard a pre-ledger document
```

### The lifecycle, now closed

```
                     ┌──────────────── withdraw ─────────────┐
                     ▼                                       │
  park ──────► Parked ──── submit ────► PendingApproval ──────┤
                 │                            │              │
                 │                       approve (last step) │
                 │                            ▼              │
                 │                         Posted ── reverse ─┴─► Reversed
                 │                            ▲
                 │                       (no rule matched)
                 │                            │
                 └──────── submit ────────────┘
                 │
            reject ──► Rejected
                 │           │
                 └─ discard ─┴──► gone (number spent, audit entry remains)
```

## The design, and why

### Discard exists because parked lines cannot be edited

`fin.TR_JournalEntryLine_NoUpdate` refuses every line update, deliberately and
at the database. That is right for a ledger, but it means there is no way to
*correct* a parked document — so before this increment, a rejected document
could only sit there forever. The honest fix is not to weaken the trigger; it is
to let a document that never reached the ledger be thrown away and re-parked.

Discard is allowed from `Draft`, `Held`, `Parked` and `Rejected`, and from
nothing else. `PendingApproval` is excluded on purpose: deleting a document out
from under an approver would leave their inbox pointing at nothing. Withdraw it
first — which is one extra call and leaves a record of who pulled it.

### The document number is not reclaimed

Gapless numbering means a number that was issued is spent. Reusing it would
defeat the thing the number range exists for, and inventing a hole to fill later
is worse. The audit entry written at discard is the only thing that explains the
gap in the sequence, which is why it is written before the delete and outlives
the document. The acceptance suite asserts that the next document takes the
*next* number, not the discarded one.

### Withdraw is the inverse of maker-checker

Only the submitter or the document's creator may withdraw — exactly the two
people maker-checker bars from approving, and for the mirror-image reason. An
approver who could withdraw a document instead of rejecting it would erase the
evidence that they ever saw it. And once any step has been approved, withdrawal
is refused outright: it would quietly undo a decision that is already on the
record. Reject it instead, on the record.

### The approve button is visible to people who cannot use it

The document's buttons are shown and hidden by status, which is a usability
decision and nothing more. Nothing is hidden by authorisation. §22.1 is explicit
that hiding a control is not authorisation, and the browser suite now proves the
point twice over: it clicks Approve as the document's own maker and asserts that
the **server** comes back with `MAKER_CHECKER_VIOLATION`.

The inbox goes further and shows *why*: a row the caller cannot release is
labelled "You submitted or created this" rather than being filtered out. An
approver needs to know a document is waiting even when somebody else must sign
it.

### The inbox navigates to the document rather than deciding in place

Approving from a list, without seeing the lines, is how rubber-stamping happens.
The inbox is deliberately thin — it lists what is waiting and opens the
document, where the decision is taken with the posting lines on screen.

### The comment prompt is a dialog, not a MessageBox

`MessageBox` cannot take free text. A rejection reason typed by the approver is
the entire value of the record, so rejection opens a real dialog with a
`TextArea` whose Confirm button stays disabled until something is typed — the
server refuses an empty rejection reason, and finding that out after a round trip
helps nobody.

## Database changes

**None.** No migration: the statuses, the workflow tables and the cascade from
header to lines were all already there. `WorkflowStatus.Withdrawn` existed in the
enum from increment 3 and was unused until now.

## A bug found by running it

**Discarding a document failed with error 50005** — "a journal line cannot be
deleted from an existing document" — from the guard that is supposed to *permit*
exactly this case.

The trigger distinguishes a legitimate cascade from an illegitimate direct
delete by checking whether the header row still exists when it fires. The handler
had loaded the lines with a tracking query in order to count them, which turned
them into tracked entities that EF removed with their own `DELETE` statements
*before* the header's — and at that moment the header was still there.

The fix is one word: `AsNoTracking()`. Untracked, EF deletes only the header and
SQL Server's cascade takes the lines with it, which is the case the trigger
exempts. It is worth knowing that a query written for counting silently changed
the delete plan.

## Verification

Four suites, **144 checks**, all passing — and, unlike increments 2 and 3, run
against the **container image** rather than the host running from source. See
[the note on the CA below](#the-image-build-behind-a-tls-inspecting-proxy).

| Suite | Checks | Was |
| --- | --- | --- |
| Posting engine (`db/tests/posting-engine.sh`) | 98 | 77 |
| Browser (`ui5/test/ui-acceptance.mjs`) | 27 | 16 |
| Database integrity (`db/tests/integrity-rules.sql`) | 14 | 14 |
| Architecture (`tests/S4HERP.ArchitectureTests`) | 5 | 5 |

```
== Withdraw ==
  PASS  A document parks and is submitted                          201
  PASS  ...and enters approval                                     200
  PASS  An approver cannot withdraw someone else's submission      403 NOT_AUTHORIZED
  PASS  The submitter can                                          200
  PASS  ...and the document is Parked again
  PASS  A withdrawn document can be submitted again                200
  PASS  ...opening a fresh approval
== Discard ==
  PASS  A pending document cannot be discarded                     422 DOCUMENT_NOT_DISCARDABLE
  PASS  ...so withdraw it first                                    200
  PASS  Someone without the delete activity is refused             403 NOT_AUTHORIZED
  PASS  A parked document can be discarded                         200
  PASS  ...reporting what it discarded
  PASS  ...and it is gone                                          404 NOT_FOUND
  PASS  ...leaving the ledger untouched
  PASS  The next document takes the next number, not the discarded one
  PASS  A posted document can never be discarded                   422 DOCUMENT_NOT_DISCARDABLE
  PASS  A rejected document can be discarded                       200
  PASS  ...closing the lifecycle dead-end
  PASS  Trial balance still foots after discards                   200
```

```
== Park, submit, approve, through the UI ==
  PASS  Parking opens the document
  PASS  A parked document offers Submit, not Reverse
  PASS  Submitting shows the approval history
  PASS  ...with the step still pending
  PASS  The maker approving their own document is refused by the server
== The approver's inbox ==
  PASS  The inbox lists the submitted document
  PASS  ...and can be opened from it
  PASS  Opening from the inbox shows the document itself
  PASS  ...with its lines, so the approver sees what they are releasing
  PASS  Approving posts the document
  PASS  ...and the approval history records who approved it
```

### Two bugs in the browser test, worth recording

Both would have read as passes if the assertions had been looser:

- The maker-checker check originally parked as `seed.accountant`, who holds no
  `W_APPROVE` at all — so the server refused with `NOT_AUTHORIZED` and a regex
  matching "refused" would have gone green while testing nothing about
  maker-checker. It now parks as `seed.supervisor`, who holds both post and
  approve, which is the only configuration in which maker-checker is the reason.
- The inbox check clicked the *first* row. The inbox legitimately contains
  documents earlier suites left pending, so it opened the wrong document and
  everything after it failed. It now selects the row for the document under
  test.

## The image build behind a TLS-inspecting proxy

Increment 3 recorded that the image could not be built here, because the sandbox
intercepts TLS and the build container does not trust its root certificate —
`dotnet restore` fails with `UntrustedRoot`, which NuGet reports as `NU1301`,
wording that sends you looking at the feed rather than at the proxy. Increment 3
was therefore verified by running the host from source.

That was a workaround for a problem real customers have. Corporate networks
routinely re-sign TLS, and an on-premise ERP is exactly the kind of software that
gets built on one. So the Dockerfile now takes an optional root certificate:

```bash
EXTRA_CA_BUNDLE=/path/to/corporate-root.crt docker compose build
docker build --secret id=extra_ca,src=/path/to/corporate-root.crt ...
```

A BuildKit secret rather than a `COPY`, so the certificate is never a layer in
the image, and optional — unset it mounts as an empty file and the guard skips
it. Verified in all three states: a real certificate, `/dev/null` (what Compose
substitutes when `EXTRA_CA_BUNDLE` is unset), and no secret passed at all.

With it, the image builds, and this increment's 144 checks were run against the
running container instead of a source run.

## Files

```
src/Modules/Workflow/S4HERP.Workflow.Contracts/ApprovalContracts.cs   WithdrawAsync, Withdrawn outcome
src/Modules/Workflow/S4HERP.Workflow.Application/ApprovalService.cs   withdrawal rules
src/Modules/Finance/S4HERP.Finance.Application/JournalApproval.cs     withdraw and discard handlers
src/Modules/Finance/S4HERP.Finance.Api/FinanceEndpoints.cs            withdraw, DELETE
src/Host/S4HERP.Host/Infrastructure/SampleDataSeeder.Business.cs      ACTVT 06 grants
ui5/webapp/view/Approvals.view.xml · controller/Approvals.controller.js
ui5/webapp/view/DocumentDisplay.view.xml · controller/DocumentDisplay.controller.js
ui5/webapp/view/JournalEntry.view.xml · controller/JournalEntry.controller.js
ui5/webapp/model/formatter.js · manifest.json · i18n/*.properties
```

## Limitations

1. **A discarded document's workflow history is orphaned.** `wf.WorkflowInstance`
   keeps the rejected or withdrawn instance, whose `ObjectId` now names a
   document that no longer exists. That is defensible as an audit record — it is
   evidence the approval happened — but nothing marks it as pointing at a deleted
   object, so a future inbox or report has to know not to follow the link.
2. **No deputy approver, escalation, reminder or notification.** Carried forward
   from increment 3 unchanged. An approver still learns about a document by
   opening the inbox.
3. **No parallel approval.** Steps remain strictly sequential.
4. **Approval rules still have no maintenance UI or API.** They are seed data,
   and they are exactly the kind of configuration whose changes should be
   audited.
5. **The launchpad tile list is still hard-coded**, now with three entries
   instead of two. `cfg.TransactionCode` and `sec.RoleTransactionCode` hold the
   registry and the grants; wiring the tiles to them is what makes the launchpad
   genuinely role-based.
6. **Still no QUnit or OPA5 tests.** The browser suite covers these journeys
   end-to-end, but unit-level control tests are owed.
7. **Discard has no bulk form.** Cleaning up a month of abandoned parked
   documents is one call each.

## Next

**AR/AP** is the remaining item from the increment-2 plan and the largest thing
still designed but not built: invoices, payments, clearing, dunning and the
payment run. Open items are now created on every path that reaches the ledger —
direct posting, no-approval-required submission, and final approval — which is
the precondition clearing needs.

Delivered by [increment 5](increment-5-ar-ap.md), except the payment run and
dunning.
