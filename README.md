# S4HERP

An S/4HANA-inspired, web-based ERP system.

| Phase | State |
| --- | --- |
| 1 — [Solution Blueprint](docs/blueprint/README.md) | Delivered. Five of its six open questions are still unanswered; the assumptions taken are listed in the Phase 2 report |
| 2 — [Database](docs/phase2/README.md) | Delivered and verified. 83 tables, row-level security, partitioning, seed data, 14/14 integrity rules passing |
| 3 — [Backend](docs/phase3/README.md) | Delivered. Posting engine, simulation, idempotency, gapless numbering, tax, reversal, authorisation, trial balance |
| 3 — [Backend, increment 3](docs/phase3/increment-3-workflow.md) | Delivered. Park, submit, approve, reject, maker-checker, multi-level approval |
| 3 — [Increment 4](docs/phase3/increment-4-lifecycle.md) | Delivered. Withdraw, discard, approvals inbox and the approval screens |
| 3 — [Increment 5](docs/phase3/increment-5-ar-ap.md) | Delivered. Payment terms, due dates, payments, clearing, reset, open items and aging |
| 3 — [Increment 6](docs/phase3/increment-6-payment-run.md) | Delivered. House banks, payment methods and the payment run (F110) |
| 3 — [Increment 7](docs/phase3/increment-7-payment-run-approval.md) | Delivered. Approval on the payment run: release threshold, maker-checker, execution gate |
| 3 — [Increment 8](docs/phase3/increment-8-payment-file.md) | Delivered. ISO 20022 pain.001 payment file, S_EXPORT on the download, bank details in the proposal |
| 3 — [Increment 9](docs/phase3/increment-9-partner-bank-maintenance.md) | Delivered. Partner bank maintenance under maker-checker; the approval engine generalised beyond financial documents |
| 3 — [Increment 10](docs/phase3/increment-10-approvals-inbox.md) | Delivered. One approvals inbox across all object types, and the bank change approval screen |
| 3 — [Increment 11](docs/phase3/increment-11-payment-run-screen.md) | Delivered. The payment run on a screen: propose, submit, approve, execute, generate and download the bank file |
| 3 — [Increment 12](docs/phase3/increment-12-bank-status.md) | Delivered. ISO 20022 pain.002 import: a payment gains a status from the bank, and a refusal can be reversed |
| 4 — [SAPUI5 front end](docs/phase4/README.md) | Delivered on OpenUI5. Launchpad, journal entry, document display, trial balance, English/Khmer. 16/16 browser checks |
| 5 — [Testing and deployment](docs/phase5/README.md) | Delivered. CI pipeline, architecture tests, deployment-time schema setup, operations guide |

**452 automated checks passing** across four suites, against the container image.
Modules still to build — bank statement import (camt.053) and reconciliation,
dunning, Asset Accounting, Controlling allocations, SE11 and SE16N — are designed
in the blueprint and listed in each phase report.

ASP.NET Core 10 over SQL Server 2025, containerised with Docker Compose.

| Component | Version | Image |
| --- | --- | --- |
| API runtime | .NET 10 (LTS) | `mcr.microsoft.com/dotnet/aspnet:10.0` |
| Build SDK | .NET 10 (LTS) | `mcr.microsoft.com/dotnet/sdk:10.0` |
| Database | SQL Server 2025 | `mcr.microsoft.com/mssql/server:2025-latest` |
| ORM | EF Core 10 | `Microsoft.EntityFrameworkCore.SqlServer` |

## Quick start

```bash
cp .env.example .env
# edit .env and set a strong MSSQL_SA_PASSWORD
docker compose up -d --build
```

On first run the host applies EF Core migrations, then the idempotent scripts in
`db/scripts/` (row-level security, partitioning, grants, immutability guards),
then seeds the §24 sample data. Readiness stays red until all of that completes.

```bash
curl http://localhost:8080/api/v1/about
curl http://localhost:8080/health/ready
```

Open <http://localhost:8080/> for the front end, or run the acceptance suites:

```bash
./db/tests/posting-engine.sh                     # 381 posting-engine checks over HTTP
node ui5/test/ui-acceptance.mjs                  # 63 browser checks
./dotnet.sh test tests/S4HERP.ArchitectureTests  # 5 architecture checks
```

Verify the database-level integrity rules:

```bash
docker compose cp db/tests/integrity-rules.sql db:/tmp/tests.sql
docker compose exec -T db /opt/mssql-tools18/bin/sqlcmd \
  -C -I -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d S4HERP -i /tmp/tests.sql
```

`-I` is required: `sqlcmd` defaults `QUOTED_IDENTIFIER` to OFF, and DML against a
filtered index fails without it.

### Building behind a TLS-inspecting proxy

Corporate networks routinely terminate and re-sign TLS. Inside `docker build`
that makes every package restore fail with `UntrustedRoot` — NuGet reports it as
`NU1301`, which reads as though the feed were down. Point `EXTRA_CA_BUNDLE` at
the proxy's root certificate:

```bash
echo 'EXTRA_CA_BUNDLE=/etc/ssl/certs/corporate-root.crt' >> .env
docker compose build
```

It is passed as a BuildKit secret, so the certificate is never a layer in the
image, and it is optional — unset, it mounts as an empty file and the build skips
it. Plain `docker build` takes the same thing directly:

```bash
docker build --secret id=extra_ca,src=/path/to/corporate-root.crt \
  -f src/Host/S4HERP.Host/Dockerfile -t s4herp-api:latest .
```

If the builder has no npm access at all, build the OpenUI5 bundle on the host and
copy it in instead:

```bash
npm --prefix ui5 install && npm --prefix ui5 run build
docker compose build --build-arg UI_SOURCE=prebuilt
```

OpenAPI document (Development environment only): <http://localhost:8080/openapi/v1.json>

Tear down, keeping the database volume:

```bash
docker compose down
```

Tear down and delete the data:

```bash
docker compose down -v
```

## Configuration

All settings come from `.env` (see `.env.example`):

| Variable | Default | Purpose |
| --- | --- | --- |
| `MSSQL_SA_PASSWORD` | _required_ | SQL Server `sa` password. Needs 8+ chars with upper, lower, digit and symbol. |
| `MSSQL_PID` | `Developer` | SQL Server edition. |
| `MSSQL_DATABASE` | `S4HERP` | Database name used in the connection string. |
| `MSSQL_PORT` | `1433` | Host port mapped to SQL Server. |
| `API_PORT` | `8080` | Host port mapped to the API. |
| `ASPNETCORE_ENVIRONMENT` | `Development` | Set to `Production` for deployments. |
| `EXTRA_CA_BUNDLE` | _unset_ | Root certificate of a TLS-inspecting proxy, trusted during the image build only. See below. |

The API reads its connection string from `ConnectionStrings__Default`, which
Compose composes from the variables above. Set
`RunMigrationsOnStartup=false` to disable the automatic migration step.

## Endpoints

| Method | Route | Description |
| --- | --- | --- |
| `GET` | `/` | Service banner |
| `GET` | `/health/live` | Liveness — the process is up |
| `GET` | `/health/ready` | Readiness — SQL Server reachable and the schema current. What the container health check probes, so `healthy` means usable and not merely listening |
| `POST` | `/api/v1/finance/journal-entries` | Post an accounting document (FB50) |
| `POST` | `/api/v1/finance/journal-entries/simulate` | Full accounting impact without posting |
| `GET` | `/api/v1/finance/journal-entries/{cc}/{year}/{no}` | Display a document (FB03) |
| `POST` | `/api/v1/finance/journal-entries/{cc}/{year}/{no}/reverse` | Reverse a document (FB08) |
| `POST` | `/api/v1/finance/journal-entries/park` | Park a document without posting (FV50) |
| `POST` | `/api/v1/finance/journal-entries/{cc}/{year}/{no}/submit` | Submit a parked document for approval |
| `POST` | `/api/v1/finance/journal-entries/{cc}/{year}/{no}/approve` | Approve the caller's step; posts on the last one |
| `POST` | `/api/v1/finance/journal-entries/{cc}/{year}/{no}/reject` | Reject, with a mandatory reason |
| `POST` | `/api/v1/finance/journal-entries/{cc}/{year}/{no}/withdraw` | Pull a submission back out of approval |
| `DELETE` | `/api/v1/finance/journal-entries/{cc}/{year}/{no}` | Discard a document that never reached the ledger |
| `GET` | `/api/v1/finance/journal-entries/{cc}/{year}/{no}/workflow` | Approval state and step history |
| `GET` | `/api/v1/approvals` | Everything waiting on the calling user, of every kind. Optional `?objectType=` |
| `GET` | `/api/v1/finance/approvals` | Journal entries waiting on the calling user |
| `POST` | `/api/v1/finance/payments` | Pay and clear open items (F-28 / F-53) |
| `POST` | `/api/v1/finance/payments/{cc}/{year}/{no}/reset-clearing` | Reopen the items a payment cleared (FBRA) |
| `GET` | `/api/v1/finance/open-items` | Open items with aging (FBL5N / FBL1N) |
| `POST` | `/api/v1/finance/payment-runs` | Propose a payment run (F110); posts nothing |
| `GET` | `/api/v1/finance/payment-runs` | Runs the caller may see, newest first. Optional `?companyCode=`, `?status=`, `?take=` |
| `GET` | `/api/v1/finance/payment-runs/{runId}` | The proposal, its payments, its exclusions and its approval trail |
| `POST` | `/api/v1/finance/payment-runs/{runId}/submit` | Send the proposal for approval |
| `POST` | `/api/v1/finance/payment-runs/{runId}/approve` | Approve the caller's step; the last one releases the run |
| `POST` | `/api/v1/finance/payment-runs/{runId}/reject` | Reject the run, with a mandatory reason |
| `POST` | `/api/v1/finance/payment-runs/{runId}/execute` | Post the proposal |
| `DELETE` | `/api/v1/finance/payment-runs/{runId}` | Discard a proposal |
| `POST` | `/api/v1/finance/payment-runs/{runId}/payment-file` | Generate the ISO 20022 pain.001 instruction |
| `GET` | `/api/v1/finance/payment-runs/{runId}/payment-file` | File metadata: counts, control sum, hash, copies taken |
| `GET` | `/api/v1/finance/payment-runs/{runId}/payment-file/content` | The XML itself. Requires `S_EXPORT`, and every read is audited |
| `GET` | `/api/v1/finance/payment-runs/{runId}/bank-status` | What the bank said about this run, latest word per transaction |
| `POST` | `/api/v1/finance/payment-status-reports` | Import an ISO 20022 pain.002 payment status report |
| `GET` | `/api/v1/finance/payment-status-reports/{messageId}` | An imported report, with the bank's verdict per payment |
| `GET` | `/api/v1/finance/payment-status-reports/rejections` | Payments the bank refused that the ledger still shows as paid |
| `POST` | `/api/v1/finance/payment-status-reports/rejections/{id}/resolve` | Reverse a refused payment and reopen its invoices |
| `GET` | `/api/v1/business-partners/{bp}/bank-details` | A partner's bank details. All of them are approved |
| `POST` | `/api/v1/business-partners/{bp}/bank-details/changes` | Raise a bank detail change for approval (FK02). Applies nothing |
| `GET` | `/api/v1/business-partners/bank-details/changes/{id}` | A change request: what it proposes, what it replaces, who must sign it |
| `POST` | `/api/v1/business-partners/bank-details/changes/{id}/approve` | Approve; the last approval applies the change |
| `POST` | `/api/v1/business-partners/bank-details/changes/{id}/reject` | Reject, with a mandatory reason |
| `POST` | `/api/v1/business-partners/bank-details/changes/{id}/withdraw` | Pull a request back before anyone has decided it |
| `GET` | `/api/v1/finance/reports/trial-balance` | Trial balance, derived from the journal |

Every call is authorised server-side and denied by default. In Development the
caller is named by an `X-S4HERP-User` header — see
[Phase 3](docs/phase3/README.md#development-mode-authentication) for how that is
gated, and why the host refuses to start if it is enabled elsewhere.

```bash
curl -X POST http://localhost:8080/api/v1/finance/journal-entries \
  -H 'X-S4HERP-User: seed.accountant' -H 'Content-Type: application/json' \
  -d '{"companyCode":"1000","documentType":"SA","documentDate":"2026-04-10",
       "postingDate":"2026-04-10","currency":"USD",
       "lines":[{"postingKey":"40","amount":250,"glAccount":"6000000000","costCenter":"CC101000"},
                {"postingKey":"50","amount":250,"glAccount":"1000100000"}]}'

curl 'http://localhost:8080/api/v1/finance/reports/trial-balance?companyCode=1000&fiscalYear=2026' \
  -H 'X-S4HERP-User: seed.accountant'
```

## Connecting to the database

```bash
docker compose exec db /opt/mssql-tools18/bin/sqlcmd \
  -C -I -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d S4HERP \
  -Q "SELECT PartnerNumber, Name FROM mdm.BusinessPartner"
```

From the host (Azure Data Studio, SSMS, DBeaver): `localhost,1433`, user `sa`,
the password from `.env`, and "Trust server certificate" enabled — the
container uses a self-signed certificate.

Row-level security applies to every tenant-scoped table, so an ad-hoc session
sees nothing until it identifies itself:

```sql
EXEC sys.sp_set_session_context N'TenantId', 1;       -- or IsCrossTenant, 1
```

## Local development without Docker

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
Start only the database in Docker and run the API on the host:

```bash
docker compose up -d db

export ConnectionStrings__Default="Server=localhost,1433;Database=S4HERP;User Id=sa;Password=<your-password>;Encrypt=True;TrustServerCertificate=True"
dotnet run --project src/Host/S4HERP.Host
```

No host SDK? `./dotnet.sh <args>` runs the .NET 10 SDK in a container:

```bash
./dotnet.sh build S4HERP.sln -c Release
```

### Migrations

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add <Name> --project src/Host/S4HERP.Host --output-dir Migrations
dotnet ef database update --project src/Host/S4HERP.Host
```

`DesignTimeDbContextFactory` supplies a placeholder connection string, so
scaffolding a migration works with no database running.

## Layout

```
.
├── compose.yaml                     api + db services, volume, health checks
├── Directory.Build.props            shared TFM and analyser settings
├── Directory.Packages.props         central package versions
├── db/
│   ├── scripts/                     RLS, partitioning, grants, immutability guards
│   └── tests/                       integrity + posting-engine acceptance suites
├── dotnet.sh · run-host.sh          SDK in a container; run the host from source
├── docs/
│   ├── blueprint/                   Phase 1 design
│   ├── phase2/                      table catalogue, ERDs, phase report
│   ├── phase3/                      posting-engine and workflow phase reports
│   └── adr/                         decisions taken after Phase 1
├── src/
│   ├── BuildingBlocks/              shared domain, application and infrastructure
│   ├── Modules/                     Organization · Security · BusinessPartner
│   │                                Finance · Controlling · Audit · Workflow
│   └── Host/S4HERP.Host/            composition root, migrations, seeder, Dockerfile
├── ui5/                             OpenUI5 front end (UI5 Tooling)
├── tests/                           architecture tests
└── .github/workflows/ci.yml         build · architecture · schema · acceptance · image
```

## Notes

- The API image runs as the non-root `app` user from the .NET base image and
  listens on port 8080 inside the container.
- The runtime image ships without `curl`, so the container `HEALTHCHECK`
  re-invokes the app binary with `--healthcheck` instead. It probes readiness,
  and the start period is 180s because on a fresh volume that window has to
  cover migrations, the post-migration scripts and the sample seed.
- Compose gates API startup on the database's health check, and the migration
  step retries with backoff, so a slow SQL Server start will not crash-loop the
  API.
- SQL Server data lives in the `mssql-data` named volume and survives
  `docker compose down`.
- `Microsoft.OpenApi` is pinned explicitly to 2.11.0 because the version
  transitively pulled in by `Microsoft.AspNetCore.OpenApi` carries a known
  advisory.
- `InvariantGlobalization` is explicitly **false**: `Microsoft.Data.SqlClient`
  throws on every connection under invariant mode.
- Tenant isolation is enforced twice — EF global query filters, and SQL Server
  row-level security driven by session context. Unset session context denies
  rather than allows.
