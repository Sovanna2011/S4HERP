# Increment 6: house banks, payment methods and the payment run

Status: **delivered and verified.** F110 — the largest remaining AR/AP piece,
named as next by [increment 5](increment-5-ar-ap.md#next).

Per §26: completed components, files, database changes, tests, limitations, next.

## What was built

| Component | State |
| --- | --- |
| `cfg.PaymentMethod` | Direction, country, whether bank details are needed |
| `fin.HouseBank` / `fin.HouseBankAccount` | The bank the company pays from, and its G/L account |
| `fin.PaymentRun` / `fin.PaymentRunItem` | Proposal, execution, and the exclusion log |
| Proposal (F110) | Selects what could be paid and records what could not, with reasons |
| Execution | One payment document per partner, clearing that partner's items |
| Discard | A proposal that was never executed, kept as evidence |

### API

```
POST   /api/v1/finance/payment-runs                  propose a run (F110). Posts nothing
GET    /api/v1/finance/payment-runs/{runId}          the proposal, its payments and exclusions
POST   /api/v1/finance/payment-runs/{runId}/execute  post it: one document per partner
DELETE /api/v1/finance/payment-runs/{runId}          discard a proposal
```

## The design, and why

### The run pays by issuing exactly the command a person would

`ExecutePaymentRunHandler` does not post anything itself. It groups the selected
items by partner and sends a `PostPaymentCommand` through the dispatcher for
each — the same command the manual F-28/F-53 endpoint sends, through the same
authorisation, validation, clearing and audit.

That is the whole point. A payment run with its own posting code would be a
second definition of what a payment is, and the two would drift: the first
divergence anyone notices is usually a clearing that behaves differently
depending on how it was triggered. Nesting works because `TransactionBehavior`
joins an open transaction rather than starting a second one, so the whole run
still commits or rolls back as one.

### A proposal that hides what it skipped is worse than no proposal

Excluded candidates are stored, not filtered away, each with a sentence saying
why: *not due until 2026-05-10*, *the partner is blocked for payment*, *the
partner does not permit payment method T*, *the item's currency differs from the
paying account's*, *no due date*.

The failure this prevents is specific and common: a supplier goes unpaid for a
month, somebody asks why, and the honest answer is "it was not in the selection"
— which is not an answer. `CK_PaymentRunItem_Exclusion` enforces at the database
that an excluded item states a reason.

### Currency mismatch is refused, not converted

Paying a USD invoice out of a THB account needs a conversion the run does not
do. It excludes the item and says so. Guessing a rate inside a payment run is how
a bank file goes out with the wrong amount on it.

### A discarded proposal is marked, not deleted

`PaymentRunStatus.Deleted` rather than a row removal. The proposal is evidence of
what was considered on a date — exactly what gets asked for when an invoice was
missed. An executed run cannot be discarded at all; undoing it means resetting
the clearings and reversing the payments, which the message says.

### Direction comes from the payment method

An outgoing method selects vendor items, an incoming one customer items. Nothing
else in the request says which subledger is in scope, and having the method carry
it means a run cannot be pointed at the wrong one by mistake.

## Database changes

Migration `PaymentRunAndHouseBanks`: five tables — `cfg.PaymentMethod`,
`fin.HouseBank`, `fin.HouseBankAccount`, `fin.PaymentRun`, `fin.PaymentRunItem`.

`HouseBank` sits in `fin` rather than `cfg` because an account points at a G/L
account and Finance owns those; Organization depends on nothing (ADR-17).

The `Down` drops `sec.TenantIsolationPolicy` first — now standard for any
migration dropping a tenant-scoped table, per
[increment 4](increment-4-lifecycle.md#two-bugs-found-by-running-it).

Three fields that have existed on `PartnerCompanyCode` since Phase 2 with nothing
behind them — `PaymentMethods`, `IsPaymentBlocked`, `DunningProcedure` — now have
two of the three actually driving behaviour. Dunning is still unbacked.

## Verification

Four suites, **208 checks**, all passing against the container image. The
payment-run block passed on its first run.

| Suite | Checks | Was |
| --- | --- | --- |
| Posting engine (`db/tests/posting-engine.sh`) | 162 | 136 |
| Browser (`ui5/test/ui-acceptance.mjs`) | 27 | 27 |
| Database integrity (`db/tests/integrity-rules.sql`) | 14 | 14 |
| Architecture (`tests/S4HERP.ArchitectureTests`) | 5 | 5 |

```
== Payment run: proposal ==
  PASS  A vendor invoice posts                                     201
  PASS  A proposal is created without posting anything             201
  PASS  ...proposing the vendor invoice
  PASS  The proposal is readable                                   200
  PASS  ...and lists the invoice due 2026-04-15
  PASS  ...and records what it left out, with reasons
  PASS  Nothing was posted by proposing                            200
== Payment run: execution ==
  PASS  Executing posts the payments                               200
  PASS  ...one document per partner
  PASS  ...and the run cannot be executed twice                    422 PAYMENT_RUN_NOT_PROPOSED
  PASS  ...nor discarded once executed                             422 PAYMENT_RUN_NOT_PROPOSED
  PASS  The paid invoice is no longer open                         200
  PASS  ...the run cleared it
  PASS  The run now shows its payment document                     200
  PASS  ...against the partner it paid
  PASS  Trial balance still foots after the run                    200
== Payment run: refusals ==
  PASS  An unknown payment method is refused                       422 UNKNOWN_PAYMENT_METHOD
  PASS  An unknown house bank account is refused                   422 UNKNOWN_HOUSE_BANK_ACCOUNT
  PASS  A clerk may not run payments in another company code       403 NOT_AUTHORIZED
  PASS  A run with nothing due proposes nothing                    201
  PASS  ...and totals zero
  PASS  ...and executing it is refused rather than posting nothing 422 PAYMENT_RUN_EMPTY
  PASS  An unexecuted proposal can be discarded                    200
  PASS  ...and is kept as evidence of what was considered
  PASS  An unknown run is not found                                404 NOT_FOUND
```

One check is worth pointing at: *"...and records what it left out, with
reasons"* fails if the exclusion list is empty. Without it the exclusion logic
could rot untested while everything else stayed green, because a run where
nothing is excluded looks exactly like a run where exclusions do not work.

## Files

```
src/Modules/Organization/S4HERP.Organization.Domain/Configuration.cs        PaymentMethod, PaymentDirection
src/Modules/Finance/S4HERP.Finance.Domain/Banking.cs                        HouseBank, PaymentRun
src/Modules/Finance/S4HERP.Finance.Application/PaymentRun.cs                propose, execute, discard, read
src/Modules/Finance/S4HERP.Finance.Infrastructure/JournalConfigurations.cs
src/Modules/Finance/S4HERP.Finance.Api/FinanceEndpoints.cs
src/Host/S4HERP.Host/Infrastructure/SampleDataSeeder.Org.cs                 three payment methods
src/Host/S4HERP.Host/Infrastructure/SampleDataSeeder.Journal.cs             a house bank per company code
src/Host/S4HERP.Host/Migrations/*_PaymentRunAndHouseBanks.cs
```

## Limitations

1. **No payment file.** The run posts the accounting and clears the items; it
   produces no SEPA, ISO 20022 or local bank format, so nothing actually reaches
   a bank. That is the next piece and it is largely a formatting problem, but it
   is not done.
2. **No approval on the run.** A proposal can be executed by whoever created it.
   The [increment 3](increment-3-workflow.md) approval engine is directly
   reusable — a run is an object with an amount and a maker — and this is the
   most valuable thing to add next, because a payment run is precisely where
   maker-checker earns its keep.
3. **No cash discount taken.** The run pays the full open amount even when a
   discount is still available, and the open-items report will happily show that
   it was. Carried over from increment 5's limitation 1: posting the discount
   needs a discount account and a gross/net decision.
4. **No partner bank details.** `PaymentMethod.RequiresBankDetails` is stored and
   the run does not yet check it, because there is no partner bank-account table
   to check against. A transfer method therefore behaves like a cheque.
5. **No dunning.** `OpenItem.DunningLevel`, `LastDunningDate` and
   `PartnerCompanyCode.DunningProcedure` all still have nothing behind them.
6. **No exception handling within a run.** If one partner's payment fails, the
   whole run rolls back. That is safe and simple; SAP would let the rest proceed
   and list the failure.
7. **No front end.** Proposal review is exactly the kind of screen a payment run
   needs, and there is none.

## Next

**Approval on the payment run** is the highest-value next step and reuses what
already exists. After that, the payment file, then dunning.

Outside AR/AP the untouched modules are unchanged: Asset Accounting, Controlling
allocations and settlement, the SE11 data dictionary, the SE16N table browser,
the custom-object framework and the integration outbox.
