# S4HERP

ASP.NET Core 10 Web API backed by SQL Server 2025, fully containerised with Docker Compose.

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

The API applies EF Core migrations on startup, so the schema is created on the
first run. Once both containers report healthy:

```bash
curl http://localhost:8080/
curl http://localhost:8080/health/ready

curl -X POST http://localhost:8080/api/products \
  -H 'Content-Type: application/json' \
  -d '{"sku":"WIDGET-001","name":"Widget","unitPrice":19.99,"quantityOnHand":100}'

curl http://localhost:8080/api/products
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

The API reads its connection string from `ConnectionStrings__Default`, which
Compose composes from the variables above. Set
`RunMigrationsOnStartup=false` to disable the automatic migration step.

## Endpoints

| Method | Route | Description |
| --- | --- | --- |
| `GET` | `/` | Service banner. |
| `GET` | `/health/live` | Liveness — process is up. |
| `GET` | `/health/ready` | Readiness — SQL Server is reachable. |
| `GET` | `/api/products` | List products. |
| `GET` | `/api/products/{id}` | Get one product. |
| `POST` | `/api/products` | Create a product. |
| `PUT` | `/api/products/{id}` | Update a product. |
| `DELETE` | `/api/products/{id}` | Delete a product. |

## Connecting to the database

```bash
docker compose exec db /opt/mssql-tools18/bin/sqlcmd \
  -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d S4HERP \
  -Q "SELECT * FROM Products"
```

From the host (Azure Data Studio, SSMS, DBeaver): `localhost,1433`, user `sa`,
the password from `.env`, and "Trust server certificate" enabled — the
container uses a self-signed certificate.

## Local development without Docker

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
Start only the database in Docker and run the API on the host:

```bash
docker compose up -d db

export ConnectionStrings__Default="Server=localhost,1433;Database=S4HERP;User Id=sa;Password=<your-password>;Encrypt=True;TrustServerCertificate=True"
dotnet run --project src/S4HERP.Api
```

### Migrations

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add <Name> --project src/S4HERP.Api --output-dir Data/Migrations
dotnet ef database update --project src/S4HERP.Api
```

`DesignTimeDbContextFactory` supplies a placeholder connection string, so
scaffolding a migration works with no database running.

## Layout

```
.
├── compose.yaml                    # api + db services, volume, health checks
├── .env.example                    # configuration template
├── S4HERP.sln
└── src/S4HERP.Api/
    ├── Dockerfile                  # multi-stage build, non-root runtime
    ├── Program.cs                  # host, health checks, startup migration
    ├── HealthProbe.cs              # curl-free container HEALTHCHECK probe
    ├── Data/                       # DbContext, migrations, health check
    ├── Endpoints/                  # minimal API endpoints
    └── Models/                     # entities
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
