using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using S4HERP.Finance.Api;
using S4HERP.Host;
using S4HERP.Host.Infrastructure;

// The container HEALTHCHECK re-invokes this binary instead of shelling out to
// curl, which the aspnet runtime image does not ship.
if (args.Contains("--healthcheck"))
{
    return await HealthProbe.RunAsync();
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

app.MapGet("/", () => Results.Ok(new
{
    service = "S4HERP",
    phase = "3 - Backend (posting engine)",
    status = "running",
    docs = "/openapi/v1.json",
}));

app.Run();

return 0;
