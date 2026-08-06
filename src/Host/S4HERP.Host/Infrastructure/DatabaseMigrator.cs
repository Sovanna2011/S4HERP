using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using S4HERP.BuildingBlocks.Infrastructure;

namespace S4HERP.Host.Infrastructure;

/// <summary>Tracks whether startup migrations and post-migration scripts have finished.</summary>
public class MigrationState
{
    public volatile bool Completed;
}

/// <summary>
/// Applies migrations, then the SQL scripts that EF cannot express (row-level
/// security, partitioning, the read-only login), in the background so the host
/// starts listening immediately. Readiness stays red until this completes.
///
/// Development only. Production applies migrations at deployment time, before
/// the new replicas start — three replicas racing to migrate is not a plan.
/// </summary>
public class DatabaseMigrator(
    IServiceScopeFactory scopeFactory,
    MigrationState state,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<DatabaseMigrator> logger) : BackgroundService
{
    private const int MaxAttempts = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("RunMigrationsOnStartup", true))
        {
            logger.LogInformation("RunMigrationsOnStartup is false; skipping migrations.");
            state.Completed = true;
            return;
        }

        for (var attempt = 1; !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<S4herpDbContext>();

                await db.Database.MigrateAsync(stoppingToken);
                logger.LogInformation("Migrations applied.");

                await RunPostMigrationScriptsAsync(db, stoppingToken);

                if (configuration.GetValue("SeedSampleData", environment.IsDevelopment()))
                {
                    var seeder = new SampleDataSeeder(db, logger);
                    await seeder.SeedAsync(stoppingToken);
                }

                state.Completed = true;
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                // SQL Server may still be starting; back off instead of crash-looping.
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)));
                logger.LogWarning("Migration attempt {Attempt}/{Max} failed ({Message}); retrying in {Delay}.",
                    attempt, MaxAttempts, ex.Message, delay);
                await Task.Delay(delay, stoppingToken);
            }
        }
    }

    /// <summary>
    /// Runs db/scripts/*.sql in name order. Each script must be idempotent — they
    /// are re-executed on every start.
    /// </summary>
    private async Task RunPostMigrationScriptsAsync(
        S4herpDbContext db, CancellationToken cancellationToken)
    {
        var directory = ResolveScriptDirectory();
        if (directory is null)
        {
            logger.LogWarning("Post-migration script directory not found; skipping.");
            return;
        }

        foreach (var file in Directory.GetFiles(directory, "*.sql").Order(StringComparer.Ordinal))
        {
            var sql = await File.ReadAllTextAsync(file, cancellationToken);
            foreach (var batch in SplitOnGo(sql))
            {
                await db.Database.ExecuteSqlRawAsync(batch, cancellationToken);
            }

            logger.LogInformation("Applied post-migration script {Script}.", Path.GetFileName(file));
        }
    }

    private static string? ResolveScriptDirectory()
    {
        foreach (var candidate in new[] { "db/scripts", "../../db/scripts", "/app/db/scripts" })
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// GO is a batch separator understood by client tools, not valid T-SQL, so
    /// scripts must be split on it before execution.
    /// </summary>
    private static IEnumerable<string> SplitOnGo(string sql) =>
        System.Text.RegularExpressions.Regex
            .Split(sql, @"^\s*GO\s*$",
                System.Text.RegularExpressions.RegexOptions.Multiline
                | System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            .Select(part => part.Trim())
            .Where(part => part.Length > 0);
}

public class DatabaseHealthCheck(S4herpDbContext db, MigrationState migrations) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await db.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Unhealthy("SQL Server is not reachable.");
            }
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQL Server connection failed.", ex);
        }

        return migrations.Completed
            ? HealthCheckResult.Healthy("SQL Server is reachable and the schema is current.")
            : HealthCheckResult.Degraded("SQL Server is reachable; schema setup is still running.");
    }
}
