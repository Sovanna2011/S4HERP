# Increment 15: reconciling on a screen

Status: **delivered and verified.** Named as next by
[increment 14](increment-14-bank-statements.md#next): camt.053 import, matching
and reconciliation existed and were API-only, which by then was the third
capability in a row with no page.

Per §26: completed components, files, database changes, tests, limitations, next.

## What was built

| Component | State |
| --- | --- |
| Bank statements screen | Imported statements, with how much of each is still unexplained |
| Statement screen | The two balances, the difference, and every movement |
| Import | Paste the camt.053 and land on the statement it just read |
| Match and set aside | From the line, with the server's refusals shown |
| Launchpad tile | FF67, alongside the other transactions |

## The design, and why

### Both balances in one place, with the gap between them

The header carries the bank's closing balance, the ledger's balance and the
difference, side by side, plus a one-word verdict. Reading a difference off two
screens is how a reconciliation gets skipped, and computing it in your head is
how it gets skipped quietly.

The statement and its reconciliation load together in one `Promise.all`. The page
is meaningless with either half missing, and two independent loads racing to
paint it is exactly the shape that produced three test races in increment 10.

### The screen does not pre-empt the server

Matching is checked server-side — the document must post to this bank account and
move this amount — and the screen offers the button regardless, showing whatever
refusal comes back. The alternative is duplicating the rules in JavaScript, where
they would drift, and the refusals are worth reading: they say which of several
things went wrong.

### Two error messages got more honest, and only the screen would have shown it

Driving the UI produced a refusal that was correct but misleading, twice over.

Matching a bank charge to a payment that had been reversed earlier in the suite
returned **"No posted document 2026/500000004 exists in company code 1000."** The
document plainly exists; it is reversed. That message sends somebody hunting for
a typo that is not there. The lookup no longer filters on status and then claims
absence — it finds the document and says `Document … is Reversed, so it is not a
movement on the account`, with a note that the bank entry probably matches the
return the bank sent back instead.

The same fault existed one level down: a document that touched the account and
netted to zero was reported as "does not post to the bank account this statement
is for", which is untrue. It now distinguishes the two cases.

Neither is a behaviour change — both requests were refused before and are refused
now. Both are the difference between an error a person can act on and one that
wastes their afternoon.

### Pasting, not uploading

The import dialog takes the XML as text. A bank feed is an integration rather
than a person with a file, and building a file picker would dress up the absence
of one. Stated as a limitation rather than hidden behind a nicer control.

## Database changes

None.

## Verification

Four suites, **529 checks**, all passing against the container image built from an
empty volume, with the browser suite clean twice consecutively.

| Suite | Checks | Was |
| --- | --- | --- |
| Posting engine (`db/tests/posting-engine.sh`) | 429 | 429 |
| Browser (`ui5/test/ui-acceptance.mjs`) | 81 | 71 |
| Database integrity (`db/tests/integrity-rules.sql`) | 14 | 14 |
| Architecture (`tests/S4HERP.ArchitectureTests`) | 5 | 5 |

The browser suite now imports a statement, watches one movement match itself by
end-to-end id, is refused an incorrect manual match, sets the remaining movement
aside with a reason, and confirms the statement drops off the outstanding list —
entirely by clicking.

Worth naming what took three attempts: the assertion about the refusal message.
It failed, I corrected the assertion, it failed again, and only then did I look at
what the server actually said rather than what I assumed it said. Both corrections
to the *message* came out of that — the test was wrong twice, and the product was
imprecise twice, and I found the second only because I stopped adjusting the
regex and read the response.

## Files

```
ui5/webapp/view/BankStatements.view.xml                             new
ui5/webapp/controller/BankStatements.controller.js                  new
ui5/webapp/view/BankStatement.view.xml                              new
ui5/webapp/controller/BankStatement.controller.js                   new
ui5/webapp/controller/Launchpad.controller.js                       FF67 tile
ui5/webapp/model/formatter.js                                       statementLineState
ui5/webapp/manifest.json
ui5/webapp/i18n/*.properties                                        en and km
src/Modules/Finance/S4HERP.Finance.Application/BankStatements.cs    two clearer refusals
ui5/test/ui-acceptance.mjs
```

## Limitations

1. **Paste, not upload.** No file picker and no bank feed. The API is the real
   interface; this is a person's stopgap.
2. **No bank clearing account**, so this still compares rather than reconciles in
   the accounting sense. A payment issued but not yet on a statement makes the
   balances differ and has to be read off the unmatched list rather than being
   modelled as a reconciliation item. Unchanged from increment 14 and still the
   biggest thing missing in this area.
3. **No suggested matches.** The screen asks for a fiscal year and document
   number typed by hand. Offering candidate documents by amount and date — as
   suggestions a person confirms, not automatic matches — is the obvious next
   usability step and is deliberately absent for now.
4. **No un-match.** A line matched in error stays matched; there is no undo, on
   the screen or in the API.
5. **No incoming-payment matching**, no statement continuity check, no camt.052
   or camt.054. Unchanged from increment 14.
6. **Still no dunning, no cash discount, no sensitive-field configuration.**

## Next

**The bank clearing account** (limitation 2). It is the difference between "these
two numbers disagree by 800" and "these two numbers disagree by 800, and here are
the three in-flight items that explain it", and it is what makes the word
reconciliation accurate. It is a chart-of-accounts change, a posting change and a
migration, so it is a real increment rather than a screen.

After that: un-match and suggested matches (limitations 3 and 4), then incoming
payments, then dunning.

Outside AR/AP the untouched modules are unchanged: Asset Accounting, Controlling
allocations and settlement, the SE11 data dictionary, the SE16N table browser,
the custom-object framework and the integration outbox. CI has still never
executed a run, and the image's default npm-based UI build stage remains
unverified — every build in this environment uses `UI_SOURCE=prebuilt`, because
the npm registry is unreachable from it.
