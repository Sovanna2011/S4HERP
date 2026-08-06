# S4HERP

An S/4HANA-inspired, web-based ERP system.

| Phase | State |
| --- | --- |
| 1 — [Solution Blueprint](docs/blueprint/README.md) | Delivered. Five of its six open questions are still unanswered; the assumptions taken are listed in the Phase 2 report |
| 2 — [Database](docs/phase2/README.md) | **Delivered and verified.** 83 tables, row-level security, partitioning, seed data, 14/14 integrity rules passing |
| 3 — Backend | Next: posting engine and G/L |
| 4 — SAPUI5 frontend | Blocked on the SAPUI5 licence question |
| 5 — Testing and deployment | — |

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
curl http://localhost:8080/
curl http://localhost:8080/health/ready
```

Verify the database-level integrity rules:

```bash
docker compose cp db/tests/integrity-rules.sql db:/tmp/tests.sql
docker compose exec -T db /opt/mssql-tools18/bin/sqlcmd \
  -C -I -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d S4HERP -i /tmp/tests.sql
```

`-I` is required: `sqlcmd` defaults `QUOTED_IDENTIFIER` to OFF, and DML against a
filtered index fails without it.

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

The API reads its connection string from `ConnectionStrings__Default`, which
Compose composes from the variables above. Set
`RunMigrationsOnStartup=false` to disable the automatic migration step.

## Endpoints

Business endpoints arrive with the posting engine in Phase 3.

| Method | Route | Description |
| --- | --- | --- |
| `GET` | `/` | Service banner |
| `GET` | `/health/live` | Liveness — the process is up |
| `GET` | `/health/ready` | Readiness — SQL Server reachable and the schema current |

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
├── dotnet.sh                        .NET 10 SDK in a container
├── db/
│   ├── scripts/                     RLS, partitioning, grants, immutability guards
│   └── tests/                       database-level integrity assertions
├── docs/
│   ├── blueprint/                   Phase 1 design
│   ├── phase2/                      table catalogue, ERDs, phase report
│   └── adr/                         decisions taken after Phase 1
└── src/
    ├── BuildingBlocks/              shared domain + infrastructure
    ├── Modules/                     Organization · Security · BusinessPartner
    │                                Finance · Controlling · Audit
    └── Host/S4HERP.Host/            composition root, migrations, seeder, Dockerfile
```

## Notes

- The API image runs as the non-root `app` user from the .NET base image and
  listens on port 8080 inside the container.
- The runtime image ships without `curl`, so the container `HEALTHCHECK`
  re-invokes the app binary with `--healthcheck` instead.
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
