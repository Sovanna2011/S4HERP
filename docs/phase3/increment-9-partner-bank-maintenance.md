# Increment 9: partner bank maintenance, under maker-checker

Status: **delivered and verified.** Named as next by
[increment 8](increment-8-payment-file.md#next): the system paid money into
accounts it had no controlled way to change, and `F_BP_BANK` was defined and
granted to nobody.

Per §26: completed components, files, database changes, tests, limitations, next.

## What was built

| Component | State |
| --- | --- |
| `mdm.BusinessPartnerBankChangeRequest` | Proposed values, staged apart from the live record |
| Create / change / deactivate | Three operations, one approval path |
| `BP_MAINTAINER` with `F_BP_BANK` | Raises changes; holds no `F_BKPF_BUK` at all, per SOD001 |
| `SOD004` | New rule: maintaining bank details and approving those changes |
| `BP_SUPERVISOR` | Deliberately violates SOD004, so maker-checker has something to catch |
| Approval engine, generalised | Client-level objects with no amount can now be approved |
| `S4HERP.Organization.Application` | New project; the number range allocator moved out of Finance |
| `S4HERP.BusinessPartner.Application` / `.Api` | New projects; the module had no layers above Infrastructure |

### API

```
GET  /api/v1/business-partners/{partnerNumber}/bank-details          live details, all approved
POST /api/v1/business-partners/{partnerNumber}/bank-details/changes  raise a change (FK02). Applies nothing
GET  /api/v1/business-partners/bank-details/changes/{requestId}      what it proposes, what it replaces
POST .../changes/{requestId}/approve                                 the last approval applies it
POST .../changes/{requestId}/reject                                  reason mandatory
POST .../changes/{requestId}/withdraw                                requester only, before any decision
```

## The design, and why

### The live record only ever holds approved values

This is the whole increment. The obvious alternative — write the change to
`PartnerBank` and flag the row unapproved — means the live record now contains
unapproved data, and every reader downstream has to remember to filter it out.
The payment run is one of those readers, and the consequence of it forgetting is
money paid into an account nobody signed off.

Staging the proposal in a separate table makes that mistake unavailable. There is
nothing to filter, so no reader can fail to filter it. The acceptance suite proves
the property rather than assuming it: a payment run proposed while a change is
pending still resolves the **approved** account, and the request's own `previous`
block still shows the account it would replace.

The 202 on the raise endpoint is the same point in the protocol — nothing about
the partner has changed yet.

### The approval engine had to be generalised first, not fudged

`IApprovalService` was built when every approvable thing was an accounting
document, so it required a company code and a currency-denominated amount. A
partner's bank details are client-level master data and have no amount at all.

The cheap move was to pass a company code the object does not belong to and a
nominal zero. That would have made the inbox claim every bank change was worth
nothing and belonged to company code 1000, and a threshold rule in USD would have
silently started governing it. Instead `CompanyCodeId`, `Amount` and `CurrencyId`
became nullable on `WorkflowInstance`, `CurrencyId` became nullable on
`ApprovalRule`, and the matcher now reads:

```csharp
&& (amount == null
    ? r.CurrencyId == null                                     // no amount test
    : r.CurrencyId == request.CurrencyId && r.FromAmount <= amount)
```

An amount-less object matches only rules that make no amount claim. Treating "no
amount" as zero would have let every threshold rule in the system fire on a bank
detail change — including the payment release rule, which would have demanded the
wrong approver.

The seeded `BP_BANK_L1` is therefore the first rule in the system with no amount
test: it matches on object type alone, so every change waits for a second person
however small it looks. That is right for this object in a way it would be wrong
for a journal entry — where a zero threshold was tried in increment 6 and
rejected, because making every routine posting wait is how approval steps become
rubber stamps.

### Segregation of duties, and a role built to break it

`BP_MAINTAINER` holds `F_BP_BANK` and **no `F_BKPF_BUK` at all**. SOD001 pairs
those two precisely because someone who can both redirect a vendor's account and
run the payment needs no accomplice. The suite checks the fence from the other
side too: treasury, who carries the file to the bank, cannot so much as *read*
bank details, and neither can the accountant who runs the payments.

New `SOD004` names the other dangerous pair — maintaining bank details and
approving those changes — at Critical, matching SOD003's logic for journal
entries. And as with `FI_SUPERVISOR` in increment 3, one seeded role deliberately
violates it: `BP_SUPERVISOR` holds `F_BP_BANK` and `W_APPROVE` together. A control
nobody in the seed can trigger is a control nobody has tested, and the suite uses
this role to prove maker-checker blocks the *person*, not the role — the
supervisor cannot approve their own request, and can approve a clerk's.

Getting that role right took two attempts, and the first attempt is instructive:
see the verification section.

### Deactivate, never delete

An account a payment has already been made to is evidence of where the money
went. Deactivation closes the validity window and leaves the row, so the aging
report, the payment file and any future statement reconciliation can still
resolve it. The suite asserts both halves: the row survives, and `isCurrent`
turns false.

### One open request per partner, enforced by an index

Two concurrent changes to the same partner would be applied in whichever order
they were approved, and the second would silently overwrite the first — with both
approvers believing they had released the change they read. A filtered unique
index (`UX_BpBankChange_OnePending`) makes that impossible rather than unlikely; a
handler check alone would let two concurrent raises both pass.

### The approver is shown what is being replaced

`PreviousValues` snapshots the live record as JSON when the request is raised.
An approver deciding on "account 0002-00-999888-7" without seeing that it replaces
"0001-00-123456-1" is not being asked a meaningful question. It is also the record
of what was overwritten, since the live row is updated in place.

## Database changes

Migration `PartnerBankMaintenance`:

- `mdm.BusinessPartnerBankChangeRequest`, with a filtered unique index for the
  one-pending rule and `Restrict` on the bank FK — losing the record of a change
  because the account it changed was later removed is exactly backwards.
- `wf.WorkflowInstance.CompanyCodeId`, `.Amount`, `.CurrencyId` and
  `wf.ApprovalRule.CurrencyId` made nullable.

`Down` drops `sec.TenantIsolationPolicy` first, and says plainly that narrowing
the workflow columns back fails on any database that has approved a client-level
object — loud, and correct, since there is no automatic way to give a bank change
a company code it never had.

`NumberRangeObject.PartnerBankChange` plus one seeded range row cover the request
identifier. No schema change: `cfg.NumberRange` already stored the object type as
an int.

## Verification

Four suites, **343 checks**, all passing against the container image built from an
empty volume.

| Suite | Checks | Was |
| --- | --- | --- |
| Posting engine (`db/tests/posting-engine.sh`) | 297 | 227 |
| Browser (`ui5/test/ui-acceptance.mjs`) | 27 | 27 |
| Database integrity (`db/tests/integrity-rules.sql`) | 14 | 14 |
| Architecture (`tests/S4HERP.ArchitectureTests`) | 5 | 5 |

### Five defects found by running it

**Three were mine, in this increment's code.**

*`NewIsDefault` was a plain `bool`.* Every other field in a change request means
"leave it alone" when omitted; this one meant "false". A request that only touched
the SWIFT code would also have quietly cleared the partner's default account — a
change nobody typed and no approver could see. Now nullable, with an assertion
that a field the request never mentioned does not move.

*`ValidFrom` was hardcoded to today on a create.* The payment run checks bank
details against the **run date**, so details entered in August could not pay an
April invoice — and the partner this increment was meant to unblock stayed
excluded, with a correct-looking message. Bank details are routinely entered after
the invoice that carried them arrived, so the date is now settable, and shown on
the request because back-dating is a decision an approver should make knowingly.

*`PartnerNumber` was `required` on a command bound from the route.* Every
correctly-formed request failed deserialisation with a 500. Route-bound fields are
now explicitly not `required`, with a comment saying why, and the decide/withdraw
bodies became a dedicated `DecisionBody` rather than reusing the command.

**One was a test that would have passed for the wrong reason.** The maker-checker
check initially expected `MAKER_CHECKER_VIOLATION` from the requester and got
`NOT_AUTHORIZED` — because `BP_MAINTAINER` holds no `W_APPROVE`, so the authority
check refuses first. Both refusals are real, but they are different refusals, and
the one the test claimed to be exercising was unreachable: **no seeded role could
have triggered maker-checker on a bank change at all.** That is what produced
`BP_SUPERVISOR` and `SOD004`.

The fix then failed a second time, which is the more interesting half: granting
`W_APPROVE` was still not enough, because an approval step names the *role* that
decides it and `BP_SUPERVISOR` is not `FI_APPROVER`. Had the first attempt been
accepted, the test would have gone green while asserting nothing — maker-checker
appearing to fire when the role check would have refused anyway. The supervisor
now holds both roles, and the suite distinguishes all three outcomes: no
authority, own work, and someone else's work.

**One was a genuine flake, in the browser suite** — and it took two attempts,
which is the point worth recording. "The approval history records who approved it"
failed roughly one run in three: the approval panel loads on its own request, so a
control appearing is not proof its text has arrived. Fixing that one assertion
moved the failure to the *previous* one, "Submitting shows the approval history",
because the same race existed twice and the first fix only made it easier to see.

Both now poll, via helpers that return false on timeout rather than throwing, so a
genuine failure still reports as one failed check instead of aborting the suite.
Confirmed by four consecutive clean runs. A suite that fails intermittently is a
suite people learn to ignore, and this one was two runs away from being that.

## Files

```
src/Modules/BusinessPartner/S4HERP.BusinessPartner.Domain/BankMaintenance.cs
src/Modules/BusinessPartner/S4HERP.BusinessPartner.Application/BankMaintenance.cs
src/Modules/BusinessPartner/S4HERP.BusinessPartner.Api/BusinessPartnerEndpoints.cs
src/Modules/BusinessPartner/S4HERP.BusinessPartner.Infrastructure/BusinessPartnerConfigurations.cs
src/Modules/Organization/S4HERP.Organization.Application/NumberRangeAllocator.cs   moved from Finance
src/Modules/Organization/S4HERP.Organization.Domain/Configuration.cs
src/Modules/Workflow/S4HERP.Workflow.Domain/Workflow.cs
src/Modules/Workflow/S4HERP.Workflow.Contracts/ApprovalContracts.cs
src/Modules/Workflow/S4HERP.Workflow.Application/ApprovalService.cs
src/Modules/Finance/S4HERP.Finance.Application/JournalApproval.cs
src/Host/S4HERP.Host/Infrastructure/SampleDataSeeder.Business.cs
src/Host/S4HERP.Host/Infrastructure/SampleDataSeeder.Org.cs
src/Host/S4HERP.Host/Migrations/*_PartnerBankMaintenance.cs
tests/S4HERP.ArchitectureTests/BoundaryTests.cs
db/tests/posting-engine.sh
ui5/test/ui-acceptance.mjs
```

The allocator moved because `cfg.NumberRange` is Organization's table and a
module may not depend on Finance to get a number. It was only in Finance because
accounting documents were the first thing to need one; bank change requests are
the second. Its error codes kept their exact string values — moving a class
between assemblies is not a reason to change what a client branches on.

## Limitations

1. **No general partner maintenance.** Only bank details are maintainable. Names,
   addresses, payment terms, reconciliation accounts, blocks and deletion flags
   are still seed-only, and `F_BP_GEN` remains granted to nobody. SOD002
   (partner creation plus vendor invoice posting) has nothing to bite on yet.
2. **No sensitive-field configuration.** SAP's T055F lets an installation choose
   which fields trigger a confirmation; here *every* bank change needs approval,
   which is safe but not configurable. An installation wanting approval on the
   account number and not on the bank's display name cannot express that.
3. **No change-document history on the bank record itself.** The request table
   holds the before-image of each approved change, so the trail exists — but it
   is one row per request, not the field-level `audit.AuditLogField` history that
   Phase 2 modelled for this purpose.
4. **One approval level.** The seeded rule has a single step. The engine supports
   more, and a real installation would likely want two for a change of account
   number versus one for a typo, but expressing that needs limitation 2.
5. **No front end.** Bank details and their approvals are API-only. The approvals
   inbox screen still shows journal entries alone, so an approver has no way to
   discover a pending bank change without knowing its request id.
6. **Still no transmission, no pain.002, no camt.053**, so an executed payment
   run is still assumed successful. Unchanged from increment 8.
7. **No dunning, no cash discount.** Unchanged from increment 6.

## Next

**A generic approvals inbox** (limitation 5) is now the most valuable thing, and
the reason has changed: the engine serves three object types and the inbox screen
shows one. `InboxAsync` already accepts an object type filter and returns
client-level items correctly — the gap is entirely in the front end, plus one
endpoint that does not filter to journal entries.

After that: sensitive-field configuration (limitations 2 and 4), then reading
pain.002 / camt.053 so payments have a status, then dunning.

Outside AR/AP the untouched modules are unchanged: Asset Accounting, Controlling
allocations and settlement, the SE11 data dictionary, the SE16N table browser,
the custom-object framework and the integration outbox. CI has still never
executed a run, and the image's default npm-based UI build stage remains
unverified — every build in this environment uses `UI_SOURCE=prebuilt`, because
the npm registry is unreachable from it.
