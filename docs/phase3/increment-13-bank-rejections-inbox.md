# Increment 13: the page a treasurer opens first

Status: **delivered and verified.** Named as next by
[increment 12](increment-12-bank-status.md#next): the rejection query and the
reversal action both existed, and nothing listed them.

Per §26: completed components, files, database changes, tests, limitations, next.

## What was built

| Component | State |
| --- | --- |
| Bank rejections screen | Everything the bank refused, across every run |
| `RejectedPaymentView`, enriched | Who was not paid, how much, in what currency |
| Unmatched rejections | Shown as needing investigation, with no action offered |
| Launchpad tile | Alongside the other transactions |

## The design, and why

### A query nobody can ask is not a feature

Increment 12 could answer "what has the bank refused that the ledger still shows
as paid". Asking it required knowing which run to open, which means the
discrepancy was discoverable only by someone who already suspected it. The page
is the whole increment: same query, same action, now reachable.

### The list had to say who, not just which document

The bank's report knows account numbers, not our partner numbers, so the raw
verdict carries neither the partner nor a name. A page of end-to-end ids and
document numbers is not something a person can triage — the first question asked
of a failed payment is whose it was, and the second is how much.

Both are now resolved from the run the payment belongs to: the partner number and
name, and the amount summed across the invoices that payment covered. The bank's
own figure is used where it gave one, because pain.002 is not obliged to echo the
amount and a list that cannot say what is at stake cannot be prioritised.

### The summary line is the point of the page

A treasurer opening this wants to know whether there is anything to do before
reading a single row, so the header says "Nothing outstanding" in green or
"Outstanding — the ledger and the bank disagree" in red. The count is computed
from the rows rather than from the filter, because with resolved ones shown the
list length is not the workload.

Resolved rejections are hidden by default and available behind a checkbox. The
page is a work queue, not a log.

### A button that always fails is not an action

A rejection the bank named for a payment this system cannot place can never be
reversed — increment 12 refuses it, correctly, because reversing a guess is what
a ledger must never do. The first version of this screen offered the reverse
button on those rows anyway.

That is exactly the fault I named in increment 11 about inactive inbox rows, made
again here. Those rows now read "Needs investigation — not matched to a payment"
and offer nothing, which is the truth: there is no action, and the row is
permanently outstanding until a person works out what the bank meant.

## Database changes

None.

## Verification

Four suites, **475 checks**, all passing against the container image built from an
empty volume, with the browser suite clean twice consecutively.

| Suite | Checks | Was |
| --- | --- | --- |
| Posting engine (`db/tests/posting-engine.sh`) | 385 | 381 |
| Browser (`ui5/test/ui-acceptance.mjs`) | 71 | 63 |
| Database integrity (`db/tests/integrity-rules.sql`) | 14 | 14 |
| Architecture (`tests/S4HERP.ArchitectureTests`) | 5 | 5 |

### The one defect, and how it was found

The unmatched-rejection button above. It surfaced because a browser assertion I
wrote — "with nothing outstanding, the page says so" — failed: the API suite
leaves behind exactly one permanently-unresolvable rejection, so the list is
never empty.

My first instinct was that the assertion was wrong, which it was. But the reason
it was wrong is that a row exists which the page was drawing as actionable and
the server would always refuse. The test was chasing the wrong thing and found
the right one, and the fix is in the product rather than the test — the
assertion now checks that the unresolvable row is listed and offered no action.

Worth noting against the last three increments' pattern of data-pinned
assertions: this one broke for the opposite reason. It assumed *less* data than
exists rather than more, and the assumption was still the problem.

## Files

```
src/Modules/Finance/S4HERP.Finance.Application/PaymentStatus.cs   partner, name, amount, currency
ui5/webapp/view/BankRejections.view.xml                           new
ui5/webapp/controller/BankRejections.controller.js                new
ui5/webapp/controller/Launchpad.controller.js                     the tile
ui5/webapp/model/formatter.js                                     dateTime
ui5/webapp/manifest.json
ui5/webapp/i18n/*.properties                                      en and km
db/tests/posting-engine.sh
ui5/test/ui-acceptance.mjs
```

## Limitations

1. **No camt.053, so this is still the instruction loop.** A payment the bank
   accepted and then returned days later produces no pain.002 and appears
   nowhere. Bank reconciliation as an activity does not exist.
2. **No investigation workflow.** An unmatched rejection says it needs
   investigation and provides no way to record one, assign it, or close it. It
   sits on the queue indefinitely.
3. **No filtering or search** on the page beyond the resolved toggle — no company
   code, no date range, no partner. Fine at ten rows, not at a thousand.
4. **No notification.** A rejection is discovered by opening the page, same as
   every other kind of work in this system.
5. **Still no transmission, no dunning, no cash discount, no sensitive-field
   configuration.**

## Next

**camt.053** (limitation 1) is now clearly the largest remaining gap in the
payment cycle and the most valuable thing left in AR/AP. It needs a bank clearing
account in the chart of accounts, statement import, and matching rules — and it
is what makes bank reconciliation possible at all. Everything built since
increment 6 has been about instructing the bank and hearing back about those
instructions; none of it reconciles to what the account actually did.

After that: dunning, then cash discount.

Outside AR/AP the untouched modules are unchanged: Asset Accounting, Controlling
allocations and settlement, the SE11 data dictionary, the SE16N table browser,
the custom-object framework and the integration outbox. CI has still never
executed a run, and the image's default npm-based UI build stage remains
unverified — every build in this environment uses `UI_SOURCE=prebuilt`, because
the npm registry is unreachable from it.
