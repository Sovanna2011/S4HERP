# Phase 5 — Testing and Deployment

Status: **delivered.** CI pipeline, architecture tests, deployment-time schema
setup, and the operational guides below.

## Test suites

| Suite | What it proves | Where |
| --- | --- | --- |
| Architecture (5 checks) | Module boundaries, domain purity, schema ownership, concurrency tokens, tenant filters | `tests/S4HERP.ArchitectureTests` |
| Database integrity (14 checks) | The §23 rules the database itself must enforce | `db/tests/integrity-rules.sql` |
| Posting engine (385 checks) | The §23 rules the engine enforces, over HTTP, including the approval workflow | `db/tests/posting-engine.sh` |
| Browser acceptance (71 checks) | The UI journeys, in Chromium against the real API | `ui5/test/ui-acceptance.mjs` |

**475 automated checks**, all passing. Every one asserts a *specific* outcome —
an error number, an HTTP status plus application error code, a rendered value —
rather than "it didn't fail". That distinction caught two false green runs during
development and is worth keeping.

### Running them

```bash
docker compose up -d --build                      # stack, schema, seed data
# On a network where npm is unreachable from inside the build:
#   npm --prefix ui5 install && npm --prefix ui5 run build
#   docker compose build --build-arg UI_SOURCE=prebuilt

./db/tests/posting-engine.sh                      # 385 posting-engine checks
node ui5/test/ui-acceptance.mjs                   # 71 browser checks
./dotnet.sh test tests/S4HERP.ArchitectureTests   # 5 architecture checks

docker compose cp db/tests/integrity-rules.sql db:/tmp/tests.sql
docker compose exec -T db /opt/mssql-tools18/bin/sqlcmd \
  -C -I -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d S4HERP -i /tmp/tests.sql
```

`-I` matters. `sqlcmd` defaults `QUOTED_IDENTIFIER` to OFF, and DML against a
filtered index fails without it — which makes a broken harness look like a
working guard.

The posting-engine suite refuses to run twice at once. Several of its checks
measure a trial-balance delta around their own postings, so two runs sharing a
database make each other fail for no product reason. It exits 2 rather than
reporting a false red.

## Continuous integration

`.github/workflows/ci.yml`, three jobs:

| Job | Steps |
| --- | --- |
| `backend` | Restore, build (warnings are errors), architecture tests, apply schema, **migrations reverse cleanly**, start the host, database integrity rules, posting-engine acceptance |
| `frontend` | Build the OpenUI5 bundle with UI5 Tooling |
| `image` | Build the container image — depends on both |

The migration-reversal step exists because a migration without a working `Down`
is a one-way door, and the time to find that out is not during a rollback. CI
applies every migration forward against a scratch database and then unwinds it
to zero.

**It used to skip the post-migration scripts, and that was a hole.** The
scratch database has no `sec.TenantIsolationPolicy`, and the policy is created
`WITH SCHEMABINDING` — so on a real database it blocks dropping any table it
covers, which is every tenant-scoped table. A rollback that CI calls clean failed
on the first real attempt (error 3729). A migration that drops a tenant-scoped
table must drop the policy first; `db/scripts/020-row-level-security.sql` rebuilds
it on the next start. See
[Phase 3 increment 3](../phase3/increment-3-workflow.md#two-bugs-found-by-running-it).
The step now runs `--migrate` on the way up, so the unwind faces the real schema.

## Deployment

### Schema setup is a separate step

```bash
dotnet S4HERP.Host.dll --migrate
```

Applies migrations and the post-migration scripts (row-level security,
partitioning, grants, immutability triggers), then exits — non-zero on failure,
so a broken schema stops the rollout before any application container starts.

Development still migrates on startup, which is right for a single container and
wrong for a cluster: three replicas racing to migrate is not a plan. Production
sets `RunMigrationsOnStartup=false` and runs the command as a deployment job.

### Building behind a TLS-inspecting proxy

`EXTRA_CA_BUNDLE` points the build at a corporate root certificate, passed as a
BuildKit secret so it is never a layer in the image. Without it, restore inside
`docker build` fails with `UntrustedRoot` — which NuGet surfaces as `NU1301`,
wording that sends you looking at the feed rather than at the proxy. This was a
real failure here, not a hypothetical: the sandbox this was developed in
intercepts TLS, and the option exists because of it.

Unset, the secret mounts as an empty file and the build skips it, so an ordinary
network pays nothing. Verified in all three states — a real certificate,
`/dev/null`, and no secret passed at all.

### Order of a release

1. Back up the database (and confirm the backup, not just the job's exit code).
2. Run `--migrate` as a job. **Stop if it fails.**
3. Roll out the new application containers.
4. Watch `/health/ready` — it stays red until the schema is current.

Migrations are additive wherever possible, so step 3 can overlap step 2 for
non-breaking changes. A breaking change needs the usual expand/contract dance
across two releases; the data dictionary's activation pipeline
([blueprint 07](../blueprint/07-data-dictionary-se11.md)) is designed to enforce
that, and is not built yet.

### Environment configuration

| Setting | Development | Production |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Development` | `Production` |
| `Authentication:Mode` | `Development` | **must not be `Development`** — the host refuses to start |
| `RunMigrationsOnStartup` | `true` | `false` |
| `SeedSampleData` | `true` | `false` |
| `ConnectionStrings__Default` | from `.env` | from a secret store, never a file in the image |
| `S4HERP_READONLY_PASSWORD` | not set | set during provisioning, for the SE16N/reporting login |

### Provisioning the read-only login

`db/scripts/040-readonly-login.sql` grants and denies, but deliberately will not
create a login with a hard-coded password from application code. Provisioning
creates it:

```sql
CREATE LOGIN [s4herp_readonly] WITH PASSWORD = N'<from the secret store>';
```

The script then attaches the user and applies the grants on the next run. Until
it exists, the script logs that it skipped — the table browser simply has no
read-only principal to use.

### Backup and recovery

- Full backup daily, log backups every 15 minutes, giving point-in-time recovery.
- **Restore tested on a schedule.** An untested backup is a hypothesis.
- The journal is partitioned by fiscal year, so a closed year can be switched out
  to an archive table as a metadata operation rather than a copy.

## Administrator notes

### Tenant isolation

Every tenant-scoped table is covered by SQL Server row-level security, driven by
session context that the application sets on each connection. An ad-hoc session
sees nothing until it identifies itself:

```sql
EXEC sys.sp_set_session_context N'TenantId', 1;
-- or, for administration:
EXEC sys.sp_set_session_context N'IsCrossTenant', 1;
```

Unset context denies rather than allows. A tool that forgets to identify itself
returns empty results, not somebody else's ledger.

### The ledger is insert-only

`fin.JournalEntryLine` refuses `UPDATE` and `DELETE` at the database, and posted
headers refuse everything except the one-time reversal back-link. This applies to
DBA sessions too. Corrections are reversals or adjustment postings — there is no
supported way to edit a posted document, by design.

### Opening and closing periods

`cfg.PostingPeriod` holds one row per (variant, account type, fiscal year,
period). Closing AR and AP while leaving G/L open is the normal month-end state
and is exactly what the per-account-type rows are for.

### Document numbers

Gapless per (tenant, company code, fiscal year, document type). Allocation
serialises on one row, so concurrent posting to the same document type in the
same company code queues. Measured: 24 concurrent postings produced 24
consecutive numbers with no gaps. If a jurisdiction does not require gaplessness
for a document type, set `cfg.NumberRange.IsGapless = 0` for that range and the
serialisation disappears.

## Limitations

1. **CI has never run.** The workflow is written against GitHub's hosted runners
   and its YAML is validated, but this environment cannot execute GitHub Actions.
   Every step it runs has been executed by hand here; the orchestration has not.
2. **The image's default UI stage is still unverified.** `docker build` defaults
   to building the OpenUI5 bundle with npm, and the verified path here was
   `--build-arg UI_SOURCE=prebuilt`, which copies a bundle built on the host —
   the same path an air-gapped customer would use. The npm path needs one CI run
   to confirm. Everything else about the image *is* verified: it builds, and all
   144 checks pass against the running container rather than a source run.
3. **No performance testing at volume.** Everything measured so far is at seed
   scale. The journal's indexes and partitioning are designed for hundreds of
   millions of rows, and that claim is untested.
4. **No integration test project.** The posting-engine and browser suites are
   shell and Node scripts driving a running system. That is genuinely
   end-to-end, but it means no test runs without a live stack, and there is no
   xUnit integration suite with Testcontainers as the blueprint proposed.
5. **The fixed migration-reversal step has not run in CI.** It now applies the
   post-migration scripts before unwinding, and the rollback it exercises was
   verified by hand against a live database — but see limitation 1: nothing here
   can execute GitHub Actions.
6. **No security review or dependency scanning step.** CI should run
   `dotnet list package --vulnerable` and fail on findings; the
   `Microsoft.OpenApi` advisory found during Phase 1 is exactly what that
   catches.
7. **No blue/green or canary story.** The release order above is a rolling
   deployment.
8. **No monitoring or alerting.** Serilog writes structured logs to the console;
   nothing collects them.
