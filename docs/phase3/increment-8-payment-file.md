# Increment 8: the payment file, and bank details that matter

Status: **delivered and verified.** Named as next by
[increment 7](increment-7-payment-run-approval.md#next): everything up to the
bank existed and nothing left the building.

Per §26: completed components, files, database changes, tests, limitations, next.

## What was built

| Component | State |
| --- | --- |
| `fin.PaymentFile` | The instruction sent to the bank, stored and hashed |
| `PaymentFileWriter` | ISO 20022 pain.001.001.09 credit transfer initiation |
| Generate / read / download | Three routes, three different permissions |
| `S_EXPORT` on the download | Seeing a run is not the same right as taking every creditor's account number |
| `FI_TREASURY` role and user | Releases files; holds no `F_BP_BANK`, per SOD001 |
| Bank details in the proposal | A method that requires them excludes a partner that has none |

### API

```
POST /api/v1/finance/payment-runs/{runId}/payment-file          generate the pain.001
GET  /api/v1/finance/payment-runs/{runId}/payment-file          metadata: counts, sum, hash, copies taken
GET  /api/v1/finance/payment-runs/{runId}/payment-file/content  the XML. S_EXPORT, audited
```

## The design, and why

### Generated once, and never again

A payment file is not a report. To a bank, a second file carrying a new message
identifier is a second instruction, and the money leaves twice. So generation is
once-only — `PAYMENT_FILE_ALREADY_GENERATED`, pointing at the stored one — and
the uniqueness is a database index on `(TenantId, PaymentRunId)`, not only a
check in the handler. Two concurrent requests would both pass a handler check.

Downloading, by contrast, is unlimited and counted. Losing a file is normal;
re-issuing one is not. The distinction is the whole point: `DownloadCount`,
`FirstDownloadedBy` and `LastDownloadedBy` answer "who has a copy", which for a
document that authorises money to move is as much a fact worth keeping as what
is in it.

### Three routes because there are three permissions

Metadata and content are separate endpoints so the authority can be separate.
Anyone who may see the run may see that a file exists, what it totals and its
hash. Taking the XML away needs `S_EXPORT` with `SCOPE = PAYMENT_FILE` — the
object the blueprint reserves for exactly this — and writes an
`AuditAction.Export` row naming the taker and the copy number.

The seeded split is deliberate and tested: the accountant who executed the run
and generated the file **cannot** download it, and neither can the approver who
released it. Only `FI_TREASURY` can. And `FI_TREASURY` holds no `F_BP_BANK`,
because SOD001 already rates "maintain vendor bank details" plus "run payments"
as Critical — whoever carries the file to the bank must not be able to change
where it pays.

### The hash is for the conversation after something goes wrong

`ContentSha256` exists so a treasurer can prove the file the bank processed is
the file this run generated. That question only ever gets asked once, and it
gets asked on a bad day.

It is also the reason nothing regenerates: a stored file with a stored hash is
evidence; a file rebuilt on demand from current master data is a reconstruction,
and would silently differ if a creditor's account changed in between.

### IBAN when there is one, `Othr/Id` when there is not

The company codes in this system are Cambodian and Thai. Neither country issues
IBANs. A writer that assumes IBAN would emit `<IBAN/>` and produce a file every
local bank rejects, so the account identifier is `IBAN` when present and
`Othr/Id` otherwise — and the acceptance suite asserts that no IBAN element
appears at all for the seeded Cambodian creditor, not merely that `Othr` does.

This is the second time the seed's geography has changed a design decision (the
first was per-currency approval thresholds in increment 3). Building for one
country and generalising later would have produced a payment file that works
nowhere the software is meant to run.

### Built with `XDocument`, not string concatenation

A creditor called *Smith & Sons* produces valid XML instead of a file the bank
silently drops. Free escaping is the entire argument, and it is enough of one.

`BtchBookg` is `false`: the bank books each transfer separately, so the statement
shows one line per supplier and the ledger reconciles item by item. Batch booking
collapses them into a total nobody can match back. `EndToEndId` is
`{companyCode}-{year}-{paymentDocument}` — what comes back on the statement, and
therefore what makes the return leg reconcilable.

`MsgId` is capped at 35 characters by the standard and a run id alone can be 40,
so the message id is company code, generation instant and an eight-character
digest of the run id: unique in practice, traceable back to the run by eye.

### Bank details are checked at proposal time, not at file time

`PaymentMethod.RequiresBankDetails` has been stored since Phase 2 with nothing
behind it. Now an outgoing transfer excludes a partner with no valid bank
details, with the reason, in the proposal's exclusion log.

The timing is the point. Catching it at file generation would be too late: the
payment is posted and the invoice cleared, so the ledger says paid and the bank
never hears of it. The generator still refuses — `PARTNER_BANK_DETAILS_MISSING`,
for details deleted between execution and generation — but by then the only
honest thing it can do is refuse.

The suite proves the exclusion is specific rather than a blanket refusal: the
same invoice, run under method `C` (cheque, which needs no account number), is
proposed and payable.

## Database changes

Migration `PaymentFile`: one table, `fin.PaymentFile`, with unique indexes on
`(TenantId, PaymentRunId)` and `(TenantId, MessageId)`.

`Content` is the only unbounded string in the schema. The model convention
otherwise forbids them — it rejected the first scaffold attempt — and the opt-out
is stated at the property rather than the convention being weakened everywhere: a
run with a thousand creditors produces a file no fixed length would survive.

`Down` drops `sec.TenantIsolationPolicy` first, standard for any migration
dropping a tenant-scoped table. It also destroys instructions actually sent to a
bank, which are evidence of why money left — the migration says so, because
rolling back a schema is not a retraction of the payments.

Migration `PaymentMethodsAreAList`: `mdm.BusinessPartnerCompanyCode.PaymentMethods`
from `nvarchar(1)` to `nvarchar(10)`. A correction, not a feature — see the
verification section.

No schema change was needed for partner bank details. `bp.PartnerBank` has
existed since Phase 2; this increment is the first thing to read it. A new
`NumberRangeObject.PaymentRun` and one seeded range row cover the run
identifier, with no schema change either.

## Verification

Four suites, **273 checks**, all passing against the container image.

| Suite | Checks | Was |
| --- | --- | --- |
| Posting engine (`db/tests/posting-engine.sh`) | 227 | 188 |
| Browser (`ui5/test/ui-acceptance.mjs`) | 27 | 27 |
| Database integrity (`db/tests/integrity-rules.sql`) | 14 | 14 |
| Architecture (`tests/S4HERP.ArchitectureTests`) | 5 | 5 |

```
== Payment file (ISO 20022 pain.001) ==
  PASS  A file cannot be generated for a rejected run              422 PAYMENT_RUN_NOT_EXECUTED
  PASS  Reading a file that was never generated says so            422 PAYMENT_FILE_NOT_GENERATED
  PASS  An executed run generates its instruction                  201
  PASS  ...one credit transfer, pain.001.001.09
  PASS  ...totalling what the run paid
  PASS  ...with a sha256 the treasurer can quote to the bank
  PASS  ...and no account number anywhere in the metadata
  PASS  Generating a second file for the same run is refused       422 PAYMENT_FILE_ALREADY_GENERATED
  PASS  The accountant who made the file may not take it away      403 NOT_AUTHORIZED
  PASS  Nor may an approver                                        403 NOT_AUTHORIZED
  PASS  Treasury, holding S_EXPORT, may                            200
  PASS  ...and it is a pain.001.001.09 document
  PASS  ...naming the creditor account, since a bank needs one
  PASS  ...as Othr/Id, because Cambodian banks issue no IBAN
  PASS  ...and no IBAN element at all
  PASS  ...with the amount and currency the run paid
  PASS  ...one credit transfer
  PASS  ...a control sum the bank can check
  PASS  ...remittance naming the invoice it settles
  PASS  ...and the paying company as debtor
  PASS  The download is counted                                    200
  PASS  ...naming treasury as the first to take a copy
  PASS  A second download is allowed and counted, not refused      200
  PASS  ...and the count reflects it                               200
  PASS  ...two copies taken
  PASS  An unknown run has no file                                 404 NOT_FOUND
== Payment method requires bank details ==
  PASS  An invoice posts for a vendor with no bank details         201
  PASS  A transfer run proposes it only to exclude it              201
  PASS  ...with the reason, not by omission
  PASS  ...and nothing to pay
  PASS  ...so there is nothing to execute                          422 PAYMENT_RUN_EMPTY
  PASS  A cheque run pays the same invoice                         201
  PASS  ...because a cheque needs no account number
  PASS  A vendor that permits no cheques is excluded by method     201
  PASS  ...naming the method, not the bank details
  PASS  Two proposals in the same second both succeed              201
  PASS  ...with different run ids
```

Three checks in that list are worth pointing at, because each exists to stop a
different kind of false green:

- *"...and no account number anywhere in the metadata"* greps the metadata
  response for the seeded account number. Without it, the claim that the XML is
  the only route to account numbers is an intention rather than a fact.
- *"...and no IBAN element at all"* is the negative half of the `Othr/Id`
  assertion. Asserting `<Othr>` is present would still pass if the writer emitted
  an empty `<IBAN/>` alongside it, which is the exact malformed file the design
  is meant to avoid.
- *"A vendor that permits no cheques is excluded by method"* keeps the older
  exclusion reachable. Making every partner accept every method to get the
  cheque comparison working would have quietly retired a rule that was being
  tested by accident.

### Two defects found by running it, neither in this increment's code

**`PaymentMethods` was one character wide.** The payment run has matched it with
a substring search since it was written — the code was always written for a list
— but the column held exactly one entry. Nothing had failed, because no seeded
partner had ever needed two. The first one that did produced
*String or binary data would be truncated* out of the seeder. Widened to ten
characters (SAP's ZWELS) in its own migration, so the schema and the code now
agree on what the field is.

**Two payment runs created in the same second collided.** The run identifier
ended in an `HHmmss` timestamp, which reads as unique and is not: two proposals
for the same company code, date and method in the same second produced the same
id, and the unique index turned that into a 500 with no useful message. It is
now allocated from a number range — not gapless, because a run id is an internal
handle rather than a document number, but unique by construction. The regression
check is two back-to-back proposals with identical parameters.

Both were found the same way as every bug in this project so far: by running the
thing, not by reading it. Neither was in the payment-file code. The second one
had been latent since increment 6 and would have surfaced first in production,
on a busy afternoon, as an unexplained 500.

## Files

```
src/Modules/Finance/S4HERP.Finance.Domain/Banking.cs                  PaymentFile
src/Modules/Finance/S4HERP.Finance.Application/PaymentFile.cs         generate, read, download, the writer
src/Modules/Finance/S4HERP.Finance.Application/PaymentRun.cs          bank-details exclusion
src/Modules/Finance/S4HERP.Finance.Infrastructure/JournalConfigurations.cs
src/Modules/Finance/S4HERP.Finance.Api/FinanceEndpoints.cs
src/Host/S4HERP.Host/Infrastructure/ModuleRegistration.cs
src/Modules/BusinessPartner/S4HERP.BusinessPartner.Domain/PartnerFacets.cs   PaymentMethods is a list
src/Modules/Organization/S4HERP.Organization.Domain/Configuration.cs  NumberRangeObject.PaymentRun
src/Host/S4HERP.Host/Infrastructure/SampleDataSeeder.Business.cs      partner bank, FI_TREASURY, S_EXPORT
src/Host/S4HERP.Host/Infrastructure/SampleDataSeeder.Org.cs           the PR number range
src/Host/S4HERP.Host/Migrations/*_PaymentFile.cs
src/Host/S4HERP.Host/Migrations/*_PaymentMethodsAreAList.cs
db/tests/posting-engine.sh                                            payment file and bank-details blocks
```

## Limitations

1. **No transmission.** The file is generated and downloaded by a person. There
   is no host-to-host channel, no EBICS, no SFTP drop, and no signing. "Reaches
   the bank" still means somebody uploads it.
2. **No status back.** pain.002 payment status reports and camt.053 statements
   are not read, so the system never learns that a transfer was rejected or
   returned. Until it does, an executed run is assumed successful, and a bounced
   payment has to be found by reconciling the bank statement by hand.
3. **One `PmtInf` block per file.** All transfers share one execution date and
   one debtor account, which is what a single run is. A file mixing execution
   dates would need several blocks; the writer does not produce them.
4. **No SEPA specialisation.** `SvcLvl/SEPA`, `ChrgBr` variants other than
   `SLEV`, and the country-specific validation rules are not implemented. The
   file is generic pain.001, which is right for the seeded geography and would
   need work for a euro-zone company code.
5. **No partner bank maintenance API.** Bank details are seeded and read; there
   is no endpoint to create or change them, so the maker-checker on the
   highest-risk master data in the system — which `PartnerBank`'s own doc comment
   promises — is not built. `F_BP_BANK` is defined and granted to nobody.
6. **No cash discount, no dunning, no front end** for any of this. Unchanged
   from increment 6.

## Next

**Partner bank maintenance with maker-checker** (limitation 5) is now the most
valuable: the system pays into accounts it has no controlled way to change, and
SOD001 is written about precisely that pair. The approval engine already serves
two object types, so a third is small.

After that: reading pain.002 / camt.053 so payments have a status (limitation 2),
then dunning, then a generic approval inbox.

Outside AR/AP the untouched modules are unchanged: Asset Accounting, Controlling
allocations and settlement, the SE11 data dictionary, the SE16N table browser,
the custom-object framework and the integration outbox.
