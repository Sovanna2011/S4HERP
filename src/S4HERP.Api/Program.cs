using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using S4HERP.Api;
using S4HERP.Api.Data;
using S4HERP.Api.Endpoints;

// The container HEALTHCHECK re-invokes this binary instead of shelling out to
// curl, which the aspnet runtime image does not ship.
if (args.Contains("--healthcheck"))
{
    return await HealthProbe.RunAsync();
}

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Connection string 'Default' is missing. Set ConnectionStrings__Default.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

builder.Services.AddSingleton<MigrationState>();
builder.Services.AddHostedService<DatabaseMigrator>();

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("sqlserver", tags: ["ready"]);
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Liveness: the process is up and serving. No checks, so it stays green while
// migrations are still running.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// Readiness: SQL Server is reachable and migrations have been applied.
// Degraded (migrations in flight) must not report ready.
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

app.MapGet("/", () => Results.Ok(new
{
    service = "S4HERP.Api",
    status = "running",
    docs = "/openapi/v1.json",
}));

app.MapProductEndpoints();

app.Run();

return 0;
