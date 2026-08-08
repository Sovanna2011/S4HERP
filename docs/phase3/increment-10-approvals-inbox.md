# Increment 10: one inbox, for everything that needs deciding

Status: **delivered and verified.** Named as next by
[increment 9](increment-9-partner-bank-maintenance.md#next): the approval engine
served three object types and the inbox showed one.

Per §26: completed components, files, database changes, tests, limitations, next.

## What was built

| Component | State |
| --- | --- |
| `GET /api/v1/approvals` | Every pending decision for the caller, of every kind |
| `IApprovalObjectDescriber` | How a module names its own objects, without Workflow learning what they are |
| Three describers | Journal entry, payment run, bank change — each in its owning module |
| Approvals screen, rebuilt | All object types, filterable, with kind and description |
| Bank change screen | Before-and-after comparison, approve and reject |
| `S4HERP.Workflow.Api` | New project; Workflow had no HTTP surface of its own |

## The design, and why

### Workflow still does not know what a bank change is

The inbox has to show a person what is waiting on them. Workflow knows only that
an object has a type and an id, so the honest row it can build alone is
`PartnerBank BNK-00000009` — correct, and useless.

The tempting fix is to let the inbox query the objects. That would make Workflow
depend on Finance and BusinessPartner, which is precisely backwards: the whole
reason approval is a separate module is that it decides *who may approve* without
knowing what approval releases (ADR-02, and the argument in
[increment 3](increment-3-workflow.md)).

So each module registers an `IApprovalObjectDescriber` for the types it owns, and
the inbox asks. Finance describes journal entries and payment runs; BusinessPartner
describes bank changes. The dependency still runs module → Workflow only.

Descriptions are fetched in batches, one call per object type, because an inbox
with forty items must not become forty round trips.

### The subtitle is the part that matters

A title tells you which object. The subtitle tells you what you are being asked:

```
Mekong Logistics Ltd (1000000002)
Account 0001-00-123456-1 → 0002-00-999888-7 · Supplier notified a new account
```

An approver scanning ten rows can see which one moves money to a new account and
which one fixes a spelling, without opening either. For a payment run the subtitle
is the payee count and method, not just the total — a run of one large payment and
a run of two hundred small ones need different scrutiny, and the total alone does
not distinguish them.

### Null amount, not zero

A bank change has no amount. The inbox reports `"amount": null` and the screen
leaves the cell blank, because `0.00` is a claim — that the item is worth nothing —
and it is false. This is the same distinction the approval engine itself had to
learn in increment 9, now carried through to what a person sees.

Company code is null for the same reason and displays as "All company codes",
which is what client-level actually means to the person reading it.

### Rows that cannot be opened do not pretend otherwise

There is no payment run screen yet. The controller holds one map of object type →
route, and both the row's appearance and the press handler read it, so a row
cannot offer navigation the handler then refuses. Payment runs list, describe, and
are plainly inactive; pressing one explains where to decide it instead.

An object type the front end has not been taught about still appears, under its
raw type name. Hiding it would hide work.

## Database changes

None. The inbox is a query over tables that already existed.

## Verification

Four suites, **380 checks**, all passing against the container image built from an
empty volume.

| Suite | Checks | Was |
| --- | --- | --- |
| Posting engine (`db/tests/posting-engine.sh`) | 321 | 297 |
| Browser (`ui5/test/ui-acceptance.mjs`) | 40 | 27 |
| Database integrity (`db/tests/integrity-rules.sql`) | 14 | 14 |
| Architecture (`tests/S4HERP.ArchitectureTests`) | 5 | 5 |

The browser suite grew by half, because this increment's deliverable *is* a
screen. It now drives a bank change end to end through the UI: the approver finds
an item they were never told the id of, opens it, reads the comparison, approves,
and watches it leave the inbox.

### One defect, and it was the interesting kind

**An approver could not read the request they were being asked to approve.**
`GetBankChangeQuery` required `F_BP_BANK`, which an approver deliberately does
not hold — that is SOD004, from the increment before. So the item appeared in
the inbox, and opening it returned 403.

The API suite had not caught it because every read in that suite was made by a
bank clerk, who holds `F_BP_BANK` and for whom the endpoint worked perfectly. It
took a screen, driven as an approver, to walk the path a real approver walks.

The endpoint now accepts either authority — maintain *or* approve — and the
reasoning is worth stating: requiring the maintainer's permission to read would
have left approvers clicking approve on a request they were not allowed to see,
which is worse than a 403. It is a control that produces the appearance of review
without the possibility of it.

### Three test races, all the same shape

- The bank change comparison was asserted with a single `innerText()` read while
  the panel was still loading.
- After changing the inbox filter, "the bank change is gone" was checked by
  `!textAppears(...)` — which is true of a list that is *about to* become correct
  as well as one that already is. Absence needs its own poll, so
  `textDisappears` now exists alongside `textAppears`.
- A `SegmentedButton` renders its items as `<li>`, not `<button>`, and the first
  version selected by index. Selecting by label instead means reordering the
  filter in the view cannot silently change which filter the test clicks.

Two real bugs were hidden behind those races while I chased them: a formatter
referenced from the view but never written (which broke rendering, leaving the
page permanently busy), and `selectedKey`'s two-way binding not having propagated
when `selectionChange` fires — so the filter reloaded the *previous* selection and
appeared not to work. Both are fixed; neither would have been visible from the
API.

### Re-runnability

Only one bank change may be open per partner, so a browser run that died halfway
poisoned every run after it. The suite now clears any leftover for its test
partner before starting and uses a per-run account number. A suite that cannot
recover from its own previous crash is a suite that gets run once.

## Files

```
src/Modules/Workflow/S4HERP.Workflow.Contracts/ApprovalContracts.cs      describer + inbox item
src/Modules/Workflow/S4HERP.Workflow.Application/ApprovalService.cs      the inbox query
src/Modules/Workflow/S4HERP.Workflow.Api/WorkflowEndpoints.cs            new project
src/Modules/Finance/S4HERP.Finance.Application/ApprovalDescribers.cs     new
src/Modules/BusinessPartner/S4HERP.BusinessPartner.Application/BankMaintenance.cs
ui5/webapp/view/Approvals.view.xml                                       rebuilt
ui5/webapp/controller/Approvals.controller.js                            rebuilt
ui5/webapp/view/BankChange.view.xml                                      new
ui5/webapp/controller/BankChange.controller.js                           new
ui5/webapp/controller/BaseController.js                                  comment dialog moved here
ui5/webapp/model/formatter.js
ui5/webapp/manifest.json
ui5/webapp/i18n/*.properties                                             en and km
db/tests/posting-engine.sh
ui5/test/ui-acceptance.mjs
```

The comment dialog moved from `DocumentDisplay` to `BaseController` because the
bank change screen needs the same thing: a free-text reason, mandatory on
rejection, disabled-until-typed. Copying it would have let the two drift, and the
rule it enforces — a rejection must state why — is not a per-screen decision.

## Limitations

1. **No payment run screen.** Payment runs appear in the inbox, described, and
   cannot be opened. Releasing one is still an API call. This is the largest
   remaining hole in the UI, and it is now the *only* approval path with no
   screen.
2. **No bulk decisions.** Each item is opened and decided individually. That is
   deliberate for now — approving from a list without seeing the detail is how
   rubber-stamping happens — but a hundred-item inbox will eventually need
   something better than a hundred navigations.
3. **No notification of any kind.** An approver learns about work by opening the
   inbox. No email, no push, no badge count on the launchpad tile.
4. **No history view.** The inbox shows what is pending. There is no screen for
   "what have I approved", which is the question an auditor asks first.
5. **Describers are registered by hand.** Three lines in `ModuleRegistration`.
   Deliberate — the list is the answer to "what can be approved" and greppable —
   but nothing fails if a new object type is added and its describer forgotten;
   the row simply shows a raw id.
6. **Still no sensitive-field configuration, no pain.002 / camt.053, no dunning,
   no cash discount.** Unchanged from increment 9.

## Next

**The payment run screen** (limitation 1). Every other approval path now has one,
and the run is the transaction that moves the most money — the odd one out should
not be that one. It needs a proposal view (payees, exclusions with reasons,
total), approve and reject, and then execute and generate-file actions for the
roles that hold them.

After that: sensitive-field configuration, then reading pain.002 / camt.053 so an
executed payment run gains a status, then dunning.

Outside AR/AP the untouched modules are unchanged: Asset Accounting, Controlling
allocations and settlement, the SE11 data dictionary, the SE16N table browser,
the custom-object framework and the integration outbox. CI has still never
executed a run, and the image's default npm-based UI build stage remains
unverified — every build in this environment uses `UI_SOURCE=prebuilt`, because
the npm registry is unreachable from it.
