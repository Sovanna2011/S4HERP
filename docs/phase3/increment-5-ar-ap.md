# Increment 5: payment terms, payments and clearing

Status: **delivered and verified.** The last item from the increment-2 plan —
AR/AP — in the slice that makes the subledger real: an invoice falls due on a
date the system derived, a payment settles it, part-payments leave the remainder
open, and an aging report says who is late.

Per §26: completed components, files, database changes, tests, limitations, next.

## What was built

| Component | State |
| --- | --- |
| `cfg.PaymentTerm` | Baseline-date rule, net days, two cash-discount tiers |
| Due-date derivation | The posting engine derives it; a caller value is an override, not the source |
| Payment (F-28 / F-53) | Bank against the reconciliation account, clearing the items it settles |
| Partial clearing | The remainder stays open, with its sign intact |
| Reset clearing (FBRA) | Reopens exactly what that clearing took |
| Open items with aging (FBL5N / FBL1N) | Buckets, days overdue, discount still available |

### API

```
POST /api/v1/finance/payments                                        pay and clear (F-28 / F-53)
POST /api/v1/finance/payments/{cc}/{yr}/{n}/reset-clearing           reopen the items (FBRA)
GET  /api/v1/finance/open-items?companyCode=&accountType=&asOf=      open items with aging
```

## The design, and why

### A due date the caller supplies is not a due date

Before this, `OpenItem.DueDate` was whatever arrived in the request, and
`PaymentTerms` was a bare four-character code with no table behind it — the seed
referenced `N030` and nothing defined it. An aging report built on that is
reporting the caller's opinion.

`cfg.PaymentTerm` now exists, and the engine derives the due date from it:
explicit input still wins, because a negotiated one-off is real, but the default
is computed. The terms themselves come from the partner's company-code facet
rather than the request, because the terms agreed with a customer live on the
customer.

An unknown payment term is now an error. Previously an unrecognised code was
simply carried through and the item had no due date at all — invisible on every
aging report, forever current.

### The payment amount is the sum of what it clears

`PostPaymentCommand` has no amount field. The operator selects open items and
optionally how much of each; the document's value is the total. This is the
difference between applying a payment and importing a bank statement: here
somebody is asserting "this money settles these invoices", and a header total
that disagreed with the selection would have to either post an unexplained
difference or silently leave money unapplied.

Over-clearing is refused rather than absorbed (`CLEARING_EXCEEDS_OPEN_AMOUNT`).
An over-payment is a real business event that needs its own posting.

### One payment settles one partner, one direction, one currency

Mixing any of the three would need several partner lines and several clearing
groups. That is a different transaction, not a bigger one, and the refusals name
which rule was broken: `ITEMS_SPAN_PARTNERS`, `ITEMS_SPAN_DIRECTIONS`,
`ITEMS_SPAN_CURRENCIES`.

### A partial clearing keeps the item's sign and names no clearing document

Open amounts are signed — a receivable is positive, a payable negative — so a
partial payment reduces the *magnitude* and leaves the sign alone. Subtracting
directly would flip a nearly-paid receivable into a payable.

A partially cleared item deliberately carries no `ClearingDocumentNumber`. It is
still open, and naming a clearing document would make an aging report believe it
was settled.

### Reset gives back what that clearing took, not the original amount

`ResetClearingHandler` adds back each `ClearingLine.ClearedAmountDocument` rather
than restoring `OriginalAmount`. Another clearing may also be holding part of the
same item, and that one is not being reset. Restoring the original would silently
un-apply somebody else's payment.

Resetting touches no journal line: clearing state lives in `fin.OpenItem`
(ADR-09), so the trial balance is unmoved by it. The acceptance suite asserts
exactly that.

### Aging buckets are data, not a chain of ifs

`Buckets` is an ordered array of (name, upper bound in days), the last
open-ended. Every customer eventually wants their own bands, and this is the one
place to change them. An item with no due date is reported as *not due* rather
than maximally overdue — it has nothing to be late against.

## Database changes

Migration `PaymentTermsAndClearing`: one table, `cfg.PaymentTerm`, with four
check constraints — non-negative net days, each discount tier complete or absent,
percentages within 0–100, and tier 2 strictly longer than tier 1 (reversed, tier
1 would shadow tier 2 for its whole window and tier 2 would be unreachable).

`fin.ClearingHeader` and `fin.ClearingLine` needed no migration: they have been
in the schema since Phase 2 and were unused until now.

The `Down` drops `sec.TenantIsolationPolicy` first, for the reason
[increment 4](increment-4-lifecycle.md#two-bugs-found-by-running-it) established:
the policy is `SCHEMABINDING` and blocks dropping any tenant-scoped table.

## Three bugs found by running it

### The seeder never advanced the number ranges

The first AR invoice died on `Violation of PRIMARY KEY constraint
'PK_JournalEntryHeader'` — duplicate `(1, 1, 2026, 200000001)`.

The seed inserts documents with hard-coded numbers (200000001 for the customer
invoice, 400000001 for the vendor one, 700000001 for the intercompany pair) and
left every number range still pointing at its start. The first runtime posting of
a seeded document type walks straight into the seeded number.

This was latent for four increments because the acceptance suite only ever posted
document type `SA`, whose range the seed never touches. Fixed by reconciling the
ranges after the journal seed: anything writing a document out of band owes the
range an update.

### A payment left its own side out of the subledger

Phase 2's integrity rule 14 — *AR open items reconcile to the journal* — failed
after a clearing reset: 1,720 in open items against 1,600 in the journal, adrift
by exactly the amount that had been reset.

The payment posted a partner line but created no open item for it. The invoice
side went down, and where the money went was invisible to the subledger. While
every clearing stayed intact the totals happened to agree; a reset reopened the
invoice with nothing to offset it and the two ledgers separated.

The fix is what SAP does and what the schema was always shaped for: a payment's
partner line is itself an open item, cleared by the same clearing that settles
the invoice. Reset then reopens *both* sides and the reconciliation holds. It is
worth noting that the rule which caught this was written in Phase 2, three
increments before the code it caught existed.

### A check constraint that is not T-SQL

The migration failed with `Incorrect syntax near '='`. The constraint that says a
discount tier must be complete was written as:

```sql
([CashDiscount1Days] IS NULL) = ([CashDiscount1Percent] IS NULL)
```

which reads perfectly and is not T-SQL. SQL Server has no boolean type, so two
predicates cannot be compared with `=`. It compiles in the C# configuration, it
scaffolds into the migration without complaint, and it fails only when the
migration actually runs — which is to say, on somebody's deployment. Spelled out
as an explicit `(both null) OR (both not null)` it works.

## Verification

Four suites, **182 checks**, all passing against the container image.

| Suite | Checks | Was |
| --- | --- | --- |
| Posting engine (`db/tests/posting-engine.sh`) | 136 | 98 |
| Browser (`ui5/test/ui-acceptance.mjs`) | 27 | 27 |
| Database integrity (`db/tests/integrity-rules.sql`) | 14 | 14 |
| Architecture (`tests/S4HERP.ArchitectureTests`) | 5 | 5 |

```
== Payment terms and due dates ==
  PASS  Seeded customer 1000000001 has open items
  PASS  An invoice on N030 terms posts                             201
  PASS  ...and its open item is due 30 days after the document date
  PASS  ...2026-04-10 + 30 = 2026-05-10
  PASS  An invoice on N014 terms posts                             201
  PASS  ...and is due 14 days out, not 30
  PASS  ...the term drives the date, not the caller
  PASS  Unknown payment terms are refused                          422 UNKNOWN_OBJECT
== Open items and aging ==
  PASS  The open-items report is served                            200
  PASS  ...and buckets a 2026-05-10 item at 51 days on 2026-06-30
  PASS  ...and reports a total overdue
  PASS  Nothing is overdue before the due date                     200
  PASS  ...totalOverdue 0 the day after invoicing
  PASS  An auditor may read the report                             200
  PASS  A clerk may not read another company code's                403 NOT_AUTHORIZED
== Payment and clearing ==
  PASS  A payment must select something                            422 NO_ITEMS_SELECTED
  PASS  Clearing more than is open is refused                      422
  PASS  ...naming CLEARING_EXCEEDS_OPEN_AMOUNT as the reason
  PASS  A partial payment clears part of an invoice                201
  PASS  ...leaving the remainder open
  PASS  A full payment clears the rest                             201
  PASS  ...and the item is settled
  PASS  The cleared item drops out of the open-items report        200
  PASS  ...and is listed again only with includeCleared
  PASS  Clearing an already-cleared item is refused                422
  PASS  ...naming ITEM_NOT_OPEN
  PASS  The payment document balances like any other               200
  PASS  ...bank against the reconciliation account
  PASS  Trial balance still foots after payments                   200
== Reset clearing ==
  PASS  Resetting reopens the items                                200
  PASS  ...reopening both sides of the clearing
  PASS  ...and the invoice is open again                           200
  PASS  ...with only the earlier partial payment still applied
  PASS  A clearing cannot be reset twice                           422 CLEARING_ALREADY_RESET
  PASS  Resetting a document that cleared nothing is refused       422 CLEARING_NOT_FOUND
  PASS  Trial balance is untouched by a clearing reset             200
  PASS  ...clearing is not a ledger fact (ADR-09), difference 0.0000
```

## Files

```
src/Modules/Organization/S4HERP.Organization.Domain/Configuration.cs        PaymentTerm, BaselineDateRule
src/Modules/Organization/S4HERP.Organization.Infrastructure/ConfigurationConfigurations.cs
src/Modules/Finance/S4HERP.Finance.Application/
    PostPayment.cs         payment, clearing, reset
    OpenItemQueries.cs     open items and aging
    PostJournalEntryHandler.cs   due-date derivation
src/Modules/Finance/S4HERP.Finance.Api/FinanceEndpoints.cs
src/Host/S4HERP.Host/Infrastructure/SampleDataSeeder.Org.cs                 five seeded terms
src/Host/S4HERP.Host/Migrations/*_PaymentTermsAndClearing.cs
```

## Limitations

1. **Cash discount is calculated but never posted.** The open-items report shows
   the percentage still available and what it is worth, and the payment engine
   ignores it: paying less than the open amount is simply a partial clearing.
   Posting the discount needs a discount G/L account per company code and a
   decision about gross versus net recording, which is a configuration question
   rather than a coding one.
2. **No payment differences and no FX gain or loss on clearing.**
   `ClearingHeader.DifferenceDocumentNumber` exists in the schema and stays null.
   A payment in a foreign currency clears at the item's own rate, so a realised
   exchange difference is not posted.
3. **No payment run (F110).** Payments are applied one at a time. A proposal
   run — select due items, group by partner and payment method, produce a
   payment file — is the next substantial piece, and it needs house banks and
   payment methods, neither of which exists.
4. **No dunning.** `OpenItem.DunningLevel` and `LastDunningDate` are in the
   schema and nothing writes them.
5. **No credit memos or down payments** as distinct transactions. Both post as
   ordinary documents today.
6. **No FB70 / FB60 invoice transactions.** A customer invoice is an ordinary
   posting with a partner line, which works and is what the suite does. Dedicated
   endpoints would mostly be convenience — deriving the revenue side from a
   product or contract is what would make them worth having.
7. **No front end.** Open items, aging and payment application have no screens;
   the API is complete and the OpenUI5 application does not use it yet.
8. **Clearing is not concurrency-safe against itself in one respect.** Two
   payments clearing the same item race on `OpenItem.RowVersion`, so the second
   fails with a 409 rather than double-clearing — correct, but it surfaces as a
   generic conflict rather than "somebody just paid this".

## Next

The payment run (F110) is the largest remaining AR/AP piece and needs house
banks, payment methods and a proposal/approval cycle — the approval engine from
[increment 3](increment-3-workflow.md) is directly reusable for the last of
those. Dunning follows the same shape.

Outside AR/AP, the untouched modules remain Asset Accounting, Controlling
allocations and settlement, the SE11 data dictionary, the SE16N table browser,
the custom-object framework and the integration outbox.
