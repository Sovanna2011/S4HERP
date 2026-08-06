# 13 — Project Structure

## 13.1 Repository layout

```
S4HERP/
├── S4HERP.sln
├── compose.yaml                     dev stack: api + SQL Server 2025
├── .env.example
├── Directory.Build.props            shared TFM, nullable, analysers, warnings-as-errors
├── Directory.Packages.props         central package version management
│
├── src/
│   ├── Host/
│   │   └── S4HERP.Host/             the single deployable
│   │       ├── Program.cs           composition root only
│   │       ├── Modules.cs           Add<Module>Module() calls
│   │       ├── Dockerfile
│   │       └── appsettings*.json
│   │
│   ├── BuildingBlocks/
│   │   ├── S4HERP.BuildingBlocks.Domain/
│   │   ├── S4HERP.BuildingBlocks.Application/    dispatcher, pipeline, contexts
│   │   ├── S4HERP.BuildingBlocks.Infrastructure/ DbContext base, outbox, jobs
│   │   └── S4HERP.BuildingBlocks.Api/            ProblemDetails, pagination, correlation
│   │
│   └── Modules/
│       ├── Organization/
│       │   ├── S4HERP.Organization.Contracts/
│       │   ├── S4HERP.Organization.Domain/
│       │   ├── S4HERP.Organization.Application/
│       │   ├── S4HERP.Organization.Infrastructure/
│       │   └── S4HERP.Organization.Api/
│       ├── BusinessPartner/         same five projects
│       ├── Finance/                 same five, plus the posting engine
│       ├── Controlling/
│       ├── Assets/
│       ├── Workflow/
│       ├── Security/
│       ├── DataDictionary/
│       ├── TableBrowser/
│       ├── Customization/
│       ├── Reporting/
│       ├── Integration/
│       └── Audit/
│
├── ui5/
│   ├── package.json                 workspace root, UI5 Tooling 4.x
│   ├── ui5-workspace.yaml
│   ├── shell/                       launchpad-style shell
│   ├── common/                      shared library: controls, formatters,
│   │                                base controller, error handling, models
│   └── apps/
│       ├── org/  bp/  gl/  journal/  ap/  ar/  assets/  co/
│       └── se11/  se16n/  admin/  reports/
│
├── tests/
│   ├── S4HERP.ArchitectureTests/    boundary rules — see below
│   ├── S4HERP.UnitTests/            per-module domain and handler tests
│   ├── S4HERP.IntegrationTests/     real SQL Server via Testcontainers
│   ├── S4HERP.ApiTests/             contract + authorisation tests
│   └── S4HERP.PostingTests/         the §23 mandatory rules
│
├── db/
│   ├── migrations/                  generated EF migrations
│   ├── scripts/                     idempotent SQL, RLS setup, partitioning
│   └── seed/                        §24 sample data
│
├── docs/
│   ├── blueprint/                   this Phase 1 blueprint
│   ├── adr/                         decisions taken after Phase 1
│   └── guides/                      administrator and user guides (Phase 5)
│
└── deploy/
    ├── ci/                          build, test, migration, drift-check pipelines
    └── environments/                dev / test / qa / prod configuration
```

## 13.2 Assembly rules

`Directory.Build.props` sets, for every project:

```
net10.0 · nullable enable · implicit usings · TreatWarningsAsErrors
InvariantGlobalization false        (Microsoft.Data.SqlClient requires ICU)
EnforceCodeStyleInBuild true
```

`Directory.Packages.props` centralises every package version. With sixty-odd
projects, per-project versions drift within a sprint and produce assembly
binding problems that take an afternoon to diagnose.

The `InvariantGlobalization false` line is not boilerplate. The scaffold in this
repository had it set to `true` — the .NET template default for a container
API — and every SQL connection failed with
`Globalization Invariant Mode is not supported`. It is set explicitly, with a
comment, so nobody re-enables it.

## 13.3 Architecture tests

The boundaries in [02](02-module-boundaries.md) are only real if something
checks them. `S4HERP.ArchitectureTests` runs in CI and asserts:

| Rule | Assertion |
| --- | --- |
| Contracts only | No module assembly references another module's `Domain`, `Application` or `Infrastructure` |
| Domain purity | `*.Domain` references no EF Core, no ASP.NET Core, no infrastructure package |
| Single writer | Only `Finance.Infrastructure` contains a write path to `fin.JournalEntryLine` |
| No cycles | The module dependency graph is acyclic |
| Handler shape | Every `ICommand` has exactly one handler; every handler has a validator or an explicit opt-out attribute |
| Endpoint authorisation | Every endpoint carries an authorisation policy or an explicit `[AllowAnonymous]` |
| No entity leakage | No EF entity type appears in a public API request or response DTO |
| Immutability | No code path calls `Update` or `Remove` on the posted-journal entity sets |

The last three catch the mistakes that are individually small and collectively
fatal — an endpoint shipped without a policy, an entity serialised straight to
the wire, an update slipped into the ledger during a hotfix.

## 13.4 Naming

| Kind | Pattern | Example |
| --- | --- | --- |
| Command | `<Verb><Noun>Command` | `PostJournalEntryCommand` |
| Query | `Get<Noun>Query` / `Search<Noun>Query` | `GetTrialBalanceQuery` |
| Handler | `<Command>Handler` | `PostJournalEntryCommandHandler` |
| Validator | `<Command>Validator` | `PostJournalEntryCommandValidator` |
| DTO | `<Noun>Request` / `<Noun>Response` | `JournalEntryResponse` |
| EF configuration | `<Entity>Configuration` | `JournalEntryLineConfiguration` |
| Permission constant | `<Module>Permissions.<Action>` | `FinancePermissions.PostJournal` |
| Integration event | `<Noun><PastTenseVerb>Event` | `JournalEntryPostedEvent` |
| UI5 view | `<Feature><Pattern>.view.xml` | `JournalEntryObjectPage.view.xml` |

## 13.5 API surface

```
/api/v1/<module>/<resource>          REST — commands and simple reads
/odata/v1/<EntitySet>                OData V4 — read-only list and report feeds
/health/live  /health/ready          probes (already implemented)
/openapi/v1.json                     OpenAPI document
/hubs/notifications                  SignalR
```

Versioning is in the path. Breaking changes get `v2`; `v1` stays until its
consumers migrate. For an ERP with external integrations, URL versioning is
worth its verbosity — header-based versioning is invisible in logs, which is
where integration problems get diagnosed.

## 13.6 Migrating the current scaffold

The repository today contains a working .NET 10 / SQL Server 2025 Docker stack
with a `Products` CRUD sample. Disposition at the start of Phase 2:

| Existing | Action |
| --- | --- |
| `compose.yaml` | Keep. Add the read-only SQL login and RLS setup |
| `src/S4HERP.Api/Dockerfile` | Move to `src/Host/S4HERP.Host/`, keep as-is — multi-stage, non-root, curl-free health probe |
| `HealthProbe.cs`, `/health/live`, `/health/ready` | Keep. Move to `BuildingBlocks.Api` |
| `DatabaseMigrator` background service | Keep for development; Phase 5 splits deployment-time migration for production |
| `DesignTimeDbContextFactory` | Keep, one per module context |
| `Products` model, endpoints, `InitialCreate` migration | **Delete.** Placeholder that contradicts this design |
| `Microsoft.OpenApi` pin at 2.11.0 | Keep, and move to `Directory.Packages.props`. Re-check when `Microsoft.AspNetCore.OpenApi` bumps its transitive pin |
| `.gitignore`, `.dockerignore`, `.env.example` | Keep |

This is open question 6 in the [index](README.md#open-questions).
