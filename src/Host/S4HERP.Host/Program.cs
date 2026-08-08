using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using S4HERP.BusinessPartner.Api;
using S4HERP.Workflow.Api;
using S4HERP.Finance.Api;
using S4HERP.Host;
using S4HERP.Host.Infrastructure;

// The container HEALTHCHECK re-invokes this binary instead of shelling out to
// curl, which the aspnet runtime image does not ship.
if (args.Contains("--healthcheck"))
{
    return await HealthProbe.RunAsync();
}

// Deployment-time schema setup: `dotnet S4HERP.Host.dll --migrate` applies
// migrations and the post-migration scripts, then exits. Production runs this as
// a job before the new replicas start, because three replicas racing to migrate
// is not a plan (blueprint 12).
if (args.Contains("--migrate"))
{
    return await MigrateCommand.RunAsync(args);
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddS4herpModules(builder.Configuration, builder.Environment);

builder.Services.AddSingleton<MigrationState>();
builder.Services.AddHostedService<DatabaseMigrator>();

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<S4herpExceptionHandler>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("sqlserver", tags: ["ready"]);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// The built OpenUI5 bundle. Served from the host so the whole system is one
// deployable and works with no outbound network access — the UI5 CDN is not a
// dependency (ADR-15).
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    // UI5 resource bundles are .properties, which has no registered MIME type,
    // and the static file middleware serves only known types. Without this every
    // i18n bundle 404s and the UI silently falls back to control defaults.
    ContentTypeProvider = new FileExtensionContentTypeProvider
    {
        Mappings =
        {
            [".properties"] = "text/plain; charset=utf-8",
            [".fragment.xml"] = "application/xml",
        },
    },
});

app.UseMiddleware<RequestContextMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Liveness: the process is up and serving. Stays green while the schema is
// still being set up.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// Readiness: SQL Server is reachable and the schema is current. Degraded — setup
// still running — must not report ready.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
    },
});

app.MapFinanceEndpoints();
app.MapBusinessPartnerEndpoints();
app.MapWorkflowEndpoints();

// The UI is served at /, so the service banner moves to its own route.
app.MapGet("/api/v1/about", () => Results.Ok(new
{
    service = "S4HERP",
    status = "running",
    docs = "/openapi/v1.json",
    ui = "/index.html",
}));

app.Run();

return 0;
