# Increment 11: the payment run, on a screen

Status: **delivered and verified.** Named as next by
[increment 10](increment-10-approvals-inbox.md#next): every approval path had a
screen except the one that moves the most money.

Per §26: completed components, files, database changes, tests, limitations, next.

## What was built

| Component | State |
| --- | --- |
| `GET /api/v1/finance/payment-runs` | Runs the caller may see, newest first |
| Payment run detail, enriched | Approval trail and "does this need approving" on the same response |
| Payment runs screen | List, filter, and a dialog that proposes a new run (F110) |
| Payment run screen | Payees, exclusions, approval trail, bank file — and every action |
| Inbox navigation | The third and last object type now opens |
| Launchpad tile | F110, alongside the other transactions |

### API

```
GET /api/v1/finance/payment-runs?companyCode=&status=&take=   the list
GET /api/v1/finance/payment-runs/{runId}                      now carries approvalSteps
                                                              and approvalRequired
```

## The design, and why

### The whole run, in one round trip

The detail response now carries `approvalSteps` and `approvalRequired` alongside
the proposal. A screen showing a run always wants all three, and fetching them
separately means three async loads racing to paint one page — which is precisely
what produced three test races in increment 10. One request, one render.

`approvalRequired` is answered by the same matcher the submit path uses, so the
screen and the server cannot disagree about whether a run needs a signature. The
alternative — show both Submit and Execute and let the server refuse one — is a
button that always fails, which is not a design.

### The list exists because ids are not how people find things

Until now the only route to a run was its id, which meant the approvals inbox or
a note on somebody's desk. Fine for the run you were told about; useless for
"what did we pay last Tuesday".

Three decisions in the list are worth naming:

- **Scoped, not filtered.** It reads only company codes the caller is authorised
  in. A list that fetches everything and hides some of it has already read it.
- **Discarded proposals are hidden by default**, and askable for by name. A
  discarded proposal is the absence of work; showing them would bury the runs
  somebody still has to act on.
- **`hasPaymentFile` is on the row**, because whether the next click is Generate
  or Download depends on it, and generating a second file is refused for good
  reason (increment 8).

### Payee count, not just the total

A run of one large payment and a run of two hundred small ones need different
scrutiny, and the total alone does not distinguish them. Both the list row and
the inbox subtitle carry the count.

### Exclusions are not a footnote

The excluded panel is always visible when there is anything in it, never behind
a toggle. Exclusions are the half of a proposal people forget to look at, and the
reason each item was left out is the entire content of a review — an invoice
missing from a run because the vendor has no bank details is a fact somebody has
to act on, not a detail.

### Execute asks once, and says what it means

Executing posts payment documents and clears open items, and there is no
un-execute. The confirmation names the amount and says plainly that there is no
undo. Everything else on the screen is reversible or refusable; this is the one
step that is neither.

### Buttons are shown; the server refuses

The download button is on screen for the accountant who generated the file, and
pressing it produces an authorisation error — because `S_EXPORT` is treasury's,
per increment 8. Hiding it would teach that the button does not exist rather than
that the caller is not authorised (§22.1). The browser suite asserts both halves:
the accountant is refused on screen, and treasury succeeds and has the copy
counted.

### `Api.request` learned to not parse

The payment file is XML; everything else is JSON. The client helper now takes
`raw`, sets `Accept` accordingly, and still parses RFC 7807 on the error path —
because a failure is JSON whatever the request asked for.

## Database changes

None.

## Verification

Four suites, **412 checks**, all passing against the container image built from an
empty volume.

| Suite | Checks | Was |
| --- | --- | --- |
| Posting engine (`db/tests/posting-engine.sh`) | 336 | 321 |
| Browser (`ui5/test/ui-acceptance.mjs`) | 57 | 40 |
| Database integrity (`db/tests/integrity-rules.sql`) | 14 | 14 |
| Architecture (`tests/S4HERP.ArchitectureTests`) | 5 | 5 |

The browser suite now drives a payment run from nothing to a bank file entirely
by clicking: propose, submit, be refused, switch user, approve, execute, generate,
be refused the file, switch user, download.

### Two test defects, both mine, both about assuming the data

**The run I proposed was not the size I thought.** The test posted a 300 USD
invoice and asserted the screen would offer Execute, since 300 is below the
1,000 USD release threshold. It offered Submit — correctly, because restricting a
run to one partner does not restrict it to one invoice, and that partner had
other items falling due. The product was right and the assertion was wrong.

**Then the same test passed once and failed on the re-run.** The first run paid
off those other items, so the second run's total was just the new invoice —
below the threshold, and the other branch. Both behaviours are correct; the test
was pinned to neither.

Fixed by posting an invoice large enough to clear the threshold on its own, so
the branch is determined by the test rather than by what happens to be open.
Confirmed by three consecutive clean runs. The lesson is the same one increment
10 recorded about flakes: a suite whose outcome depends on its own previous run
is a suite that will be believed exactly once.

There is also a correction to how the maker-checker case is described on this
screen. The accountant who raises a run holds no `W_APPROVE` at all, and the
run's amount is checked against the approver's limit before the workflow is
consulted — so the refusal they get is authority, not maker-checker. The browser
test asserts what actually happens and says so; maker-checker on a payment run
remains proven in the API suite, by the one seeded role that could otherwise
self-release.

## Files

```
src/Modules/Finance/S4HERP.Finance.Application/PaymentRun.cs      list query, enriched detail
src/Modules/Finance/S4HERP.Finance.Api/FinanceEndpoints.cs
src/Host/S4HERP.Host/Infrastructure/ModuleRegistration.cs
ui5/webapp/view/PaymentRuns.view.xml                              new
ui5/webapp/controller/PaymentRuns.controller.js                   new
ui5/webapp/view/PaymentRun.view.xml                               new
ui5/webapp/controller/PaymentRun.controller.js                    new
ui5/webapp/controller/Approvals.controller.js                     the third destination
ui5/webapp/controller/Launchpad.controller.js                     F110 tile
ui5/webapp/model/Api.js                                           raw responses
ui5/webapp/model/formatter.js
ui5/webapp/manifest.json
ui5/webapp/i18n/*.properties                                      en and km
db/tests/posting-engine.sh
ui5/test/ui-acceptance.mjs
```

## Limitations

1. **The proposal dialog is a form, not a value help.** Company code, payment
   method, house bank and account are typed as free text with sensible defaults.
   Nothing validates them client-side; a typo becomes a server error naming what
   does not exist. That is honest but it is not usable, and every one of those
   fields has a table behind it that could populate a dropdown.
2. **No editing a proposal.** SAP lets a payables clerk block or unblock
   individual items inside a proposal before releasing it. Here a proposal is
   accepted or discarded whole, so an unwanted item means discarding, fixing the
   cause, and re-proposing.
3. **No list paging or search.** Fifty runs by default, two hundred maximum, and
   no way to search by payee or amount.
4. **No approval history view** anywhere in the UI. The inbox shows pending work,
   and a decided run shows its own trail — but "what did I approve last month" has
   no screen. Unchanged from increment 10.
5. **No notifications.** Unchanged from increment 10.
6. **Still no sensitive-field configuration, no pain.002 / camt.053, no dunning,
   no cash discount.** An executed run is still assumed successful.

## Next

**Reading pain.002 and camt.053** (limitation 6) is now the most valuable. Every
step up to the bank has a screen and a control; what comes back has neither. A
rejected transfer is currently discoverable only by reconciling a statement by
hand, and the ledger says paid the whole time. That is the last structural gap in
the payment cycle.

After that: value help on the proposal dialog (limitation 1), item-level blocking
inside a proposal (limitation 2), then dunning.

Outside AR/AP the untouched modules are unchanged: Asset Accounting, Controlling
allocations and settlement, the SE11 data dictionary, the SE16N table browser,
the custom-object framework and the integration outbox. CI has still never
executed a run, and the image's default npm-based UI build stage remains
unverified — every build in this environment uses `UI_SOURCE=prebuilt`, because
the npm registry is unreachable from it.
