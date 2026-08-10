# Increment 14: what the account actually did

Status: **delivered and verified.** Named as next by
[increment 13](increment-13-bank-rejections-inbox.md#next): everything built
since increment 6 described instructions this system sent and what the bank said
about them, and none of it reconciled to what the account did.

Per §26: completed components, files, database changes, tests, limitations, next.

## What was built

| Component | State |
| --- | --- |
| `fin.BankStatement` / `BankStatementLine` | An imported camt.053, stored whole, movement by movement |
| Automatic matching | On the end-to-end id this system put on the outgoing instruction |
| Manual match | With checks that keep it a fact rather than an opinion |
| Set aside | Bank charges and the like, with a mandatory reason |
| Bank reconciliation | Statement closing balance against the G/L, and what stands between them |

### API

```
POST /api/v1/finance/bank-statements                                  import a camt.053
GET  /api/v1/finance/bank-statements                                  imported statements, newest first
GET  /api/v1/finance/bank-statements/{id}                             the statement and every movement
GET  /api/v1/finance/bank-statements/{id}/reconciliation              bank vs ledger, and the gap
POST /api/v1/finance/bank-statements/{id}/lines/{n}/match             tie a line to a posted document
POST /api/v1/finance/bank-statements/{id}/lines/{n}/ignore            needs no document of ours
```

## The design, and why

### The first thing that describes the account rather than our instructions

pain.001 says what we asked for. pain.002 says whether the bank accepted the
asking. Neither says what the account holds. A transfer accepted in a pain.002
and returned a week later — beneficiary bank bounces it, account closed in the
interim — appears in a camt.053 and nowhere else.

That is why this is the last structural piece of the payment cycle rather than a
nicety: without it, "the ledger agrees with the bank" was an assumption nobody
could check.

### Matching only on the identifier we control

Lines are matched automatically on `EndToEndId`, which is
`{companyCode}-{year}-{paymentDocument}` — set that way in increment 8 precisely
so the return leg could be matched. Nothing else is attempted.

Matching on amount and date is the obvious next heuristic and is deliberately
absent. Two payments of the same amount on the same day are ordinary, and a
reconciliation that quietly pairs the wrong ones is worse than one that visibly
leaves both unmatched: the first hides a discrepancy behind a green tick, the
second puts it in front of a person.

### A manual match is checked, not merely recorded

Pinning a line to a document by hand verifies two things first: that the document
actually posts to *this* bank G/L account, and that it moves exactly the amount
the line moves, in the same direction. Without those, the feature records an
opinion — and the whole value of a reconciliation is that it records a fact.

The statement itself is never edited. A statement that has been "corrected" is no
longer evidence.

### "Set aside" is not "matched"

Bank charges, interest, transfers between our own accounts: real movements that
correspond to no document of ours. Marking one `Ignored` with a mandatory reason
is a different fact from matching it, and keeping them apart is what makes the
reconciliation reviewable — "somebody looked and decided this needs nothing"
should never be indistinguishable from "this is document 123".

A matched line cannot be set aside, which stops the mechanism being used to make
a real payment disappear.

### The balances are the bank's, not our arithmetic

Opening and closing balances are read from the statement's `Bal` blocks and
stored, rather than derived from the entries. The point of a statement is that it
is the bank's assertion; recomputing it would replace the assertion with our own
sum and lose the ability to notice that the two disagree. A debit balance keeps
its sign, because 500 overdrawn is not 500 in hand.

The reconciliation compares that closing balance against the G/L bank account,
summed from the journal on or before the statement date — derived, for the same
reason the trial balance is derived: a maintained balance is a second copy of the
truth, and copies drift.

### Idempotent, and account-checked

The statement id is unique, so re-importing is a no-op rather than doubling every
movement on the account. A statement for an account this system does not hold is
refused outright, matched on IBAN *or* account number because Cambodian banks
issue no IBAN — the same reason the outgoing file writes `Othr/Id`.

## Database changes

Migration `BankStatements`: two tables, with an index on
`(TenantId, Status)` for "what on this account is unreconciled" — the question a
month-end close is blocked on — and one on `EndToEndId` so automatic matching
does not scan every line ever imported. `Content` is unbounded, the same
deliberate opt-out as the other two bank documents.

`Down` says what it destroys: the ledger survives untouched, but every
reconciliation decision — which document a movement corresponds to, which
movements somebody examined and set aside — is lost.

## Verification

Four suites, **519 checks**, all passing against the container image built from an
empty volume.

| Suite | Checks | Was |
| --- | --- | --- |
| Posting engine (`db/tests/posting-engine.sh`) | 429 | 385 |
| Browser (`ui5/test/ui-acceptance.mjs`) | 71 | 71 |
| Database integrity (`db/tests/integrity-rules.sql`) | 14 | 14 |
| Architecture (`tests/S4HERP.ArchitectureTests`) | 5 | 5 |

The strongest check in the block is worth naming: the suite reads the ledger
balance the system reports, then imports a second statement asserting exactly
that closing balance, and requires it to reconcile with a zero difference. That
proves the comparison is arithmetic rather than a flag — a hardcoded expected
balance would have proved only that the seed had not changed.

### One defect, and it was mine, and I had been warned

Fifteen checks failed on the first run, from a single cause: the payment run the
statement was built around cleared the release threshold and therefore needed
approval, so nothing was executed, so there was no payment document, so the
statement's first entry had an empty amount and was skipped, so nothing matched
and every subsequent line-level test addressed a line that did not exist.

The assumption — that restricting a run to one partner restricts it to one
invoice — is **the same one that broke a browser test twice in increment 11**,
and I wrote it into a comment there. Writing it down did not stop me repeating
it. The test now submits and approves the run, and says why in a comment that
points at the increment where the lesson was first paid for.

The cascade is itself the interesting part: one wrong setup assumption produced
fifteen failures across four unrelated features, none of which was broken. A
suite that fails loudly in the wrong place is still better than one that passes
quietly, but it is worth noticing how far a bad fixture travels.

## Files

```
src/Modules/Finance/S4HERP.Finance.Domain/Banking.cs                statement + line
src/Modules/Finance/S4HERP.Finance.Application/BankStatements.cs    reader, import, match, reconcile
src/Modules/Finance/S4HERP.Finance.Infrastructure/JournalConfigurations.cs
src/Modules/Finance/S4HERP.Finance.Api/FinanceEndpoints.cs
src/Host/S4HERP.Host/Infrastructure/ModuleRegistration.cs
src/Host/S4HERP.Host/Migrations/*_BankStatements.cs
db/tests/posting-engine.sh
```

## Limitations

1. **No posting from the statement.** A proper bank ledger uses a clearing
   account: payments post to bank clearing, and the statement clears it to the
   bank account. Here payments post straight to the bank G/L account and the
   statement only *compares*. That means a payment issued but not yet on a
   statement makes the two balances differ, and the difference has to be read
   off the unmatched list rather than being modelled as reconciliation items.
   This is the single biggest thing still missing, and it is a chart-of-accounts
   change as much as a code one.
2. **No screen.** Import, matching and reconciliation are API-only. Given the
   last two increments, this is the obvious next thing and I am naming it rather
   than pretending otherwise.
3. **No incoming-payment matching.** A customer receipt on the statement matches
   nothing: there is no end-to-end id on money arriving, and matching a credit to
   an open customer item needs its own rules (reference parsing, amount and
   partner heuristics). Every credit line lands unmatched.
4. **No multi-statement continuity check.** Nothing verifies that one statement's
   opening balance equals the previous one's closing, which is the cheapest
   possible detection of a missing statement.
5. **No camt.052 or camt.054**, so nothing intraday and no separate debit/credit
   notification.
6. **Still no transmission, no dunning, no cash discount, no sensitive-field
   configuration.**

## Next

**The bank reconciliation screen** (limitation 2) — import, the matched and
unmatched lines, the difference, and the match and set-aside actions. Two
increments running have shown that a capability without a page is a capability
nobody can use.

After that, **the bank clearing account** (limitation 1), which is what turns
this from a comparison into a reconciliation in the accounting sense. Then
incoming-payment matching, then dunning.

Outside AR/AP the untouched modules are unchanged: Asset Accounting, Controlling
allocations and settlement, the SE11 data dictionary, the SE16N table browser,
the custom-object framework and the integration outbox. CI has still never
executed a run, and the image's default npm-based UI build stage remains
unverified — every build in this environment uses `UI_SOURCE=prebuilt`, because
the npm registry is unreachable from it.
