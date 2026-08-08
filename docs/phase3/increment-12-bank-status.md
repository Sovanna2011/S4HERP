# Increment 12: what the bank said back

Status: **delivered and verified.** Named as next by
[increment 11](increment-11-payment-run-screen.md#next): every step up to the
bank had a screen and a control, and what came back had neither.

Per §26: completed components, files, database changes, tests, limitations, next.

## What was built

| Component | State |
| --- | --- |
| `fin.PaymentStatusReport` / `PaymentStatusItem` | The bank's verdict, stored whole and per transaction |
| pain.002 import | Idempotent, matched to the file it answers |
| Outstanding rejections | The query this increment exists to make answerable |
| Resolve a rejection | Reverses the payment and reopens the invoices, on a person's say-so |
| Run bank status | The latest word per transaction, on the run's own screen |

### API

```
POST /api/v1/finance/payment-status-reports                             import a pain.002
GET  /api/v1/finance/payment-status-reports/{messageId}                 one report, verdict per payment
GET  /api/v1/finance/payment-status-reports/rejections                  refused, and still shown as paid
POST /api/v1/finance/payment-status-reports/rejections/{id}/resolve     reverse and reopen
GET  /api/v1/finance/payment-runs/{runId}/bank-status                   latest word per transaction
```

## The design, and why

### The ledger was lying, and nothing could tell it so

An executed payment run posted payment documents and cleared invoices. If the
bank refused a transfer, none of that changed: the ledger said paid, the invoice
stayed closed, and the only way to find out was somebody reconciling a statement
by hand. Every control in increments 6 to 11 protected the outbound leg of a
cycle whose return leg did not exist.

The import closes it. A pain.002 naming a rejected transaction produces a row on
a list called "payments the bank refused that the ledger still shows as paid" —
which is the sentence the system previously could not say.

### Importing is clerical; reversing is accounting

The import does **not** reverse anything on its own. It would be easy to make it
do so, and wrong: a bank file arriving at 3am would then post to the ledger
unattended, reversing documents nobody had looked at on the strength of an XML
file whose provenance is an email attachment.

So the import records and surfaces; a person decides. `resolve` is a separate,
authorised, audited action that composes the two operations that already exist —
reset clearing (FBRA) and reverse (FB08) — rather than reimplementing them, so
"what reversing a payment means" cannot acquire a second definition that drifts
from the first.

Order matters inside it and is not arbitrary: the invoices must be released
before the payment document is reversed, or the reversal leaves an open item
pointing at a document that no longer exists.

### A report for a message we never sent is refused

`OrgnlMsgId` must match a `PaymentFile.MessageId` this system generated. The
alternative is filing somebody else's bank traffic — or a forgery — against our
payments. Nothing is stored when it does not match.

### Re-importing is a no-op, not a duplicate

The report's own `MsgId` is unique. Bank files get re-sent by mail servers,
retried by operators, and picked up twice by anything that polls; a second set of
verdicts on the same payments would double the rejection count and make the
outstanding list wrong. The endpoint answers 200 with `alreadyImported: true`
rather than 201, because importing the same file twice is a normal thing to do
and is not a creation.

### The bank's vocabulary, not ours

`TxSts` is kept as the standard's distinction. `ACCP` and `ACSC` both mean the
transfer is going ahead, but only the second says it settled, and a treasurer
chasing a payment needs to know which one they were told. An unrecognised code
becomes `Unknown` and keeps the row — a code this parser has not seen is
information, and discarding the row would lose the payment it referred to.

The parser is namespace-agnostic on purpose. Banks send pain.002.001.10, .03 and
older; refusing a file because its minor version is not the one we generate would
reject perfectly good bank traffic.

### A verdict we cannot place is kept, never guessed at

If `OrgnlEndToEndId` does not resolve to a payment document, the row is stored
with a null document and counted as `unmatched`. Resolving it is refused
outright. Reversing "whatever that probably was" is precisely the guess a ledger
must never make, and an unmatched verdict means either the bank is confused or we
are — both need a person, and neither needs a reversal.

### The bank may revise itself

`GET /payment-runs/{runId}/bank-status` returns the **latest** word per
transaction, not every word: pending on Monday and settled on Tuesday is one
payment with a current status, not two facts of equal standing. Every report is
still stored whole, so the history is intact; this is the answer to "where does
this run stand".

Ordered by import time rather than the report's own creation date — a bank that
back-dates a correction is still telling you something later than what it told
you before.

## Database changes

Migration `PaymentStatusReports`: two tables. `Content` is unbounded, the same
deliberate opt-out as the outgoing file and for the same reason — when a payment
is disputed, what the bank actually sent is the evidence, and a summary
reconstructed from parsed rows is not it.

`IX_PaymentStatusItem_Outstanding` covers `(Status, IsResolved)`, which is the
question asked by both the morning list and the screen.

`Down` drops the tenant isolation policy first, and says what it destroys: the
reversals survive as ordinary accounting documents so the ledger stays right, but
the *reason* those reversals happened is lost, and that is the part an auditor
asks about.

## Verification

Four suites, **452 checks**, all passing against the container image built from an
empty volume.

| Suite | Checks | Was |
| --- | --- | --- |
| Posting engine (`db/tests/posting-engine.sh`) | 381 | 336 |
| Browser (`ui5/test/ui-acceptance.mjs`) | 63 | 57 |
| Database integrity (`db/tests/integrity-rules.sql`) | 14 | 14 |
| Architecture (`tests/S4HERP.ArchitectureTests`) | 5 | 5 |

The API suite proves the accounting: the invoice is cleared, the bank refuses,
the rejection is outstanding, resolving it reopens the invoice, and the trial
balance still foots. The browser suite proves a person can do it — the rejection
appears on the run with the bank's own words, a warning says the ledger still
disagrees, and one button with a mandatory reason puts it right.

### One defect, and I had made it before

`ResolveRejectedPaymentCommand.EndToEndId` was marked `required` while being
supplied from the route, so System.Text.Json rejected every well-formed request
with a 500. **This is the same mistake as increment 9**, made again three
increments later, in spite of a comment in the other module explaining exactly
why not to.

The fix this time is structural rather than a comment: the endpoint binds a
dedicated `ResolveRejectionBody` that has no route fields on it at all, so the
mistake is not available to make. That is what the increment 9 fix should have
been.

Two test assertions were also wrong, both by pinning a number the data decides:
`itemsReopened` was asserted as 1 when the payment had cleared two items, and an
open-item amount was asserted as `2500.0000` when JSON serialises `decimal(19,4)`
as `2500.0`. Both now assert the property rather than the datum — that items were
reopened, and that a 2,500 row came back open. That is the third increment
running in which a data-pinned assertion broke, which is enough of a pattern to
name: assert what the code must do, not what today's seed happens to produce.

## Files

```
src/Modules/Finance/S4HERP.Finance.Domain/Banking.cs               status report + item
src/Modules/Finance/S4HERP.Finance.Application/PaymentStatus.cs    parser, import, resolve, queries
src/Modules/Finance/S4HERP.Finance.Infrastructure/JournalConfigurations.cs
src/Modules/Finance/S4HERP.Finance.Api/FinanceEndpoints.cs
src/Host/S4HERP.Host/Infrastructure/ModuleRegistration.cs
src/Host/S4HERP.Host/Migrations/*_PaymentStatusReports.cs
ui5/webapp/view/PaymentRun.view.xml                                the bank status panel
ui5/webapp/controller/PaymentRun.controller.js
ui5/webapp/model/formatter.js
ui5/webapp/i18n/*.properties                                       en and km
db/tests/posting-engine.sh
ui5/test/ui-acceptance.mjs
```

## Limitations

1. **No camt.053.** The bank statement is not read, so this closes the
   *instruction* loop and not the *cash* loop. A payment the bank accepted and
   then returned days later — a beneficiary bank bouncing it — produces no
   pain.002 and is still invisible. Bank reconciliation as an activity does not
   exist: there is no statement import, no bank clearing account, no matching.
2. **No transmission, still.** Files arrive by whatever means a person uses to
   get them onto their machine, and are pasted into an API call. No EBICS, no
   SFTP polling, no signature verification — the import trusts that the XML came
   from the bank because it names a message we sent.
3. **No partial rejection within a payment.** A payment document covering several
   invoices is reversed whole. That matches how the file was written — one
   transaction per payment document — but a bank that split one would not be
   modelled.
4. **`AddtlInf` and `Prtry` only.** Structured reason detail beyond the code and
   one free-text line is discarded.
5. **No screen for the outstanding list.** The rejections query exists and the
   run screen shows its own; there is no "everything the bank has refused across
   all runs" page, which is the one a treasurer would open first each morning.
6. **Still no sensitive-field configuration, no dunning, no cash discount.**

## Next

**A bank-status inbox** (limitation 5) is the cheapest real win: the query
exists, the resolve action exists, and what is missing is one screen listing
rejections across runs. A treasurer should not have to remember which run to
open.

After that, **camt.053** (limitation 1) is the larger and more valuable piece —
it needs a bank clearing account, statement import and matching rules, and it is
what makes bank reconciliation possible at all. Then dunning.

Outside AR/AP the untouched modules are unchanged: Asset Accounting, Controlling
allocations and settlement, the SE11 data dictionary, the SE16N table browser,
the custom-object framework and the integration outbox. CI has still never
executed a run, and the image's default npm-based UI build stage remains
unverified — every build in this environment uses `UI_SOURCE=prebuilt`, because
the npm registry is unreachable from it.
