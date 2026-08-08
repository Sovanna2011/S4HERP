# Phase 3 — Backend, increment 1: the posting engine

Status: **delivered and verified.** This is the walking skeleton from the
[roadmap](../blueprint/14-roadmap.md#m0--walking-skeleton) — one thin vertical
slice through every layer, so the architecture is proven before breadth is built
on top of it.

Per §26: completed components, files, database changes, tests, limitations, next.

**Later increments** are reported separately, and this document is left as the
record of what increment 1 delivered rather than edited to match today:

| Increment | Report |
| --- | --- |
| 2 — tax calculation and reversal (FB08) | commit `6ec8b4e` |
| 3 — park, submit, approve, maker-checker | [increment-3-workflow.md](increment-3-workflow.md) |
| 4 — withdraw, discard, approval screens | [increment-4-lifecycle.md](increment-4-lifecycle.md) |
| 5 — payment terms, payments, clearing, aging | [increment-5-ar-ap.md](increment-5-ar-ap.md) |
| 6 — house banks, payment methods, payment run | [increment-6-payment-run.md](increment-6-payment-run.md) |
| 7 — approval on the payment run | [increment-7-payment-run-approval.md](increment-7-payment-run-approval.md) |
| 8 — ISO 20022 payment file, partner bank details | [increment-8-payment-file.md](increment-8-payment-file.md) |

## What was built

| Component | State |
| --- | --- |
| Dispatcher and pipeline (ADR-05, no MediatR) | Commands, queries, four ordered behaviours |
| Central posting engine | The full §5.4 pipeline: authorise → resolve → derive period → derive lines → check open → balance → allocate → write |
| Simulation | Same code path, stops before allocation |
| Idempotency | Replay returns the original document |
| Gapless numbering (ADR-11) | Row-locked allocation inside the posting transaction |
| Currency translation | Per line, with inverse-rate fallback |
| CO derivation | Cost centre → profit centre → segment |
| Authorisation enforcer | Value-carrying authorisation objects, deny by default |
| RFC 7807 errors | Stable error codes, field-level violations, correlation id |
| Trial balance | Derived from the journal, not a maintained balance table |
| Document display | FB03 equivalent |

### API

```
POST /api/v1/finance/journal-entries              post a document          (FB50)
POST /api/v1/finance/journal-entries/simulate     full impact, no posting
GET  /api/v1/finance/journal-entries/{cc}/{yr}/{n}  display a document      (FB03)
GET  /api/v1/finance/reports/trial-balance        trial balance
```

## Verification

`db/tests/posting-engine.sh` — **25/25 passing**. Each check asserts the specific
HTTP status *and* application error code, because "did not succeed" is also what
a typo in a URL produces.

```
== Posting engine ==
  PASS  Balanced document posts                                201
  PASS  Simulation returns the impact without posting          200
  PASS  Simulation reports posted=false
  PASS  Unbalanced document is rejected                        422 DOCUMENT_UNBALANCED
  PASS  Closed period is rejected                              422 PERIOD_CLOSED
  PASS  Direct posting to a reconciliation account is rejected 422
  PASS  ...and names RECONCILIATION_ACCOUNT_DIRECT_POSTING
  PASS  Expense account without a cost object is rejected      422
  PASS  Unknown G/L account is rejected                        422
== Idempotency ==
  PASS  First request with an idempotency key posts            201
  PASS  Replay with the same key does not post again           201
  PASS  ...and returns the original document
== Authorisation ==
  PASS  Unknown user is refused                                403 NOT_AUTHORIZED
  PASS  No user header at all is refused                       403 NOT_AUTHORIZED
  PASS  Clerk may post in their own company code               201
  PASS  Clerk may not post in another company code             403 NOT_AUTHORIZED
  PASS  Clerk may not post a document type they lack           403 NOT_AUTHORIZED
  PASS  Auditor may read                                       200
  PASS  Auditor may not post, whatever their roles say         403 NOT_AUTHORIZED
== Reporting ==
  PASS  Document is readable after posting                     200
  PASS  Unknown document returns not found                     404 NOT_FOUND
  PASS  Trial balance is served                                200
  PASS  Trial balance foots to zero (0.0000)
== Derivation ==
  PASS  Profit centre and segment derive from the cost centre  200
  PASS  ...CC101000 derived PC9000 and segment CORP
                                                               25/25 passed
```

Plus the Phase 2 database rules, still 14/14 (`db/tests/integrity-rules.sql`).

### Gaplessness under concurrency

ADR-11 claims gapless numbering survives concurrent posting. Measured, not
assumed — 24 simultaneous postings against one number range:

| Measure | Result |
| --- | --- |
| Documents in range | 30 |
| Distinct document numbers | 30 |
| Range `CurrentNumber` | 100000029 |
| Highest document number | 100000029 |
| **Gaps** | **0** |

The other half of ADR-11 also held: roughly eight *rejected* postings during the
acceptance run consumed **no** numbers, because allocation happens after every
validation and derivation. That is the whole reason the lock is taken last.

## Decisions and corrections

### The trial balance was wrong, and the fix was a modelling fix

The first run reported a trial balance difference of −26.40 rather than zero.
The report inner-joins lines to `GLAccount`, and customer and vendor lines
carried no `GLAccountId` — so the subledger side of every invoice was silently
excluded. −26.40 was exactly the seeded customer and vendor lines netted.

The fix is what ACDOCA does: **a subledger line now carries its reconciliation
account** in `GLAccountId`, resolved from the partner's company-code facet during
posting. The G/L view is then complete from the journal alone, with no join to
the partner master, and "subledger reconciles to G/L" stays an invariant rather
than becoming a report-time reconstruction. Direct posting to a reconciliation
account is still refused — only the subledger may put a value there.

### `CK_User_Credential` from Phase 2 was wrong

It required interactive users to have a local password hash. That blocks every
OIDC user, and the blueprint plans OIDC for federated tenants. Replaced with
`ExternalIdentityProvider` / `ExternalSubjectId` columns and a check that accepts
either a local password **or** a federated identity. Migration `ExternalIdentity`.

### Identity resolution must be cross-tenant

Every request returned 403 on the first run. The cause was correct behaviour
producing a wrong outcome: the middleware looked up the caller on the
request-scoped context, whose connection opens with the tenant still unset, so
row-level security hid `sec.User` and no caller was ever resolved.

The tenant is not knowable until the user is, so identity resolution is
inherently cross-tenant. It now runs on `SystemDbContextFactory`, the one place
elevation lives — deliberately not a settable flag on the request context, which
would put an elevation path inside the request pipeline.

### The DI container caught a captive dependency

The host refused to start because the singleton exception handler consumed the
scoped request context — exactly the failure mode §22.1 says to detect. The
handler now reads the correlation id from the response header the middleware
already set. Worth noting that `ValidateOnBuild` caught this at startup rather
than at the first error, which is when it would otherwise have surfaced.

### Two test-harness bugs that read as product bugs

Both are recorded because they are the interesting kind of failure:

- A trial-balance difference of `0.0000` was compared as a string against `"0"`
  and reported as a failure. Fixed to compare numerically.
- In Phase 2, `sqlcmd` defaults `QUOTED_IDENTIFIER` to OFF, so DML against
  filtered indexes failed and my `CATCH` counted it as the guard working. Both
  suites now assert specific error codes.

## Development-mode authentication

Real authentication is not built yet. In Development the caller is named by an
`X-S4HERP-User` header, which is a genuine hole, so it is gated three ways:

1. `Authentication:Mode` must be set to `"Development"` explicitly — the default
   is `"None"`, which authenticates nobody and therefore authorises nothing.
2. The header is only read in that mode.
3. **The host refuses to start** if the mode is `"Development"` while
   `ASPNETCORE_ENVIRONMENT` is not — a stray environment variable must not become
   an authentication bypass.

Seeded users have no credential. Their `ExternalIdentityProvider` is
`development-header`, which is the truth about how they authenticate.

| User | Role | Can |
| --- | --- | --- |
| `seed.accountant` | FI_ACCOUNTANT | Post in 1000, 1100, 2000; any document type |
| `seed.clerk` | FI_CLERK_1000 | Post in 1000 only, document type SA only |
| `seed.auditor` | AUDITOR (type Auditor) | Read only, enforced at the user-type level |

## Files

```
src/BuildingBlocks/S4HERP.BuildingBlocks.Application/
    Abstractions.cs          ICommand/IQuery, handlers, RequiresAuthorization
    Dispatcher.cs            ADR-05 replacement for MediatR
    PipelineBehaviors.cs     logging · authorisation · validation · transaction
    Errors.cs                typed failures with stable codes and field violations
src/Modules/Security/S4HERP.Security.Application/
    AuthorizationEnforcer.cs value-carrying authorisation objects
src/Modules/Finance/S4HERP.Finance.Application/
    PostJournalEntry.cs      command, line input, derived result
    PostJournalEntryHandler.cs   the posting engine
    FiscalPeriodService.cs   period derivation and open checks
    NumberRangeAllocator.cs  ADR-11 gapless allocation
    CurrencyTranslator.cs    per-line translation
    Queries.cs               trial balance, document display
src/Modules/Finance/S4HERP.Finance.Api/FinanceEndpoints.cs
src/Host/S4HERP.Host/Infrastructure/
    RequestContext.cs        identity, tenant, correlation
    SystemDbContextFactory.cs   the one elevation path
    ProblemDetailsMapping.cs RFC 7807
db/tests/posting-engine.sh   the 25 acceptance checks
```

## Limitations

1. **No workflow.** Documents post straight to `Posted`. Park, hold, submit,
   approve and maker-checker are modelled in the schema but not implemented, so
   the approval statuses are currently unreachable.
2. **No reversal endpoint.** The schema supports it and the database enforces the
   write-once back-link; FB08 itself is not built.
3. **No tax calculation.** A tax code can be recorded on a line, but the engine
   does not compute tax or generate tax lines. Tax amounts must currently be
   entered as explicit lines.
4. **One ledger.** Postings go to the leading ledger only. `LedgerId` is on every
   line, so adding parallel ledgers is a loop, not a redesign.
5. **No AR/AP processes.** Invoices post as journal entries; payment runs,
   clearing, dunning and aging are not built.
6. **Assets, allocations, settlement** are not built.
7. **No SE11/SE16N.** They depend on the data dictionary, which is not populated.
8. **No FluentValidation validators registered yet.** The behaviour is wired and
   runs, but the posting engine validates and derives in one pass, so its rules
   live in the handler. Simple CRUD commands will use validators.
9. **Trial balance has no pagination.** Fine at seed scale, not at production
   scale — §22.1 requires server-side paging for large lists.
10. **Authorisation caching is 30 seconds.** A revoked role stays effective for
    up to that long. Acceptable, but it is a real window and should be an
    explicit configuration decision.

## Next

Increment 2 of Phase 3, in dependency order:

1. **Reversal (FB08)** and **document park/hold** — completes the document
   lifecycle the schema already models. *(Done: increment 2 and 3.)*
2. **Tax calculation** and automatic tax line generation. *(Done: increment 2.)*
3. **Workflow and maker-checker**, which the SoD rules already describe.
   *(Done: [increment 3](increment-3-workflow.md).)*
4. **AR/AP**: invoices, payments, clearing, the payment run.
   *(Done: [increment 5](increment-5-ar-ap.md),
   [increment 6](increment-6-payment-run.md) and
   [increment 7](increment-7-payment-run-approval.md) and
   [increment 8](increment-8-payment-file.md). Dunning remains.)*

The [roadmap](../blueprint/14-roadmap.md) is otherwise unchanged.
