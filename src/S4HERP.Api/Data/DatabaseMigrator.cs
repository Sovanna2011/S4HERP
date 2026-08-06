using Microsoft.EntityFrameworkCore;

namespace S4HERP.Api.Data;

/// <summary>Tracks whether startup migrations have finished.</summary>
public class MigrationState
{
    public volatile bool Completed;
}

/// <summary>
/// Applies EF Core migrations in the background so the host starts listening
/// immediately. Readiness stays red until this completes.
/// </summary>
public class DatabaseMigrator(
    IServiceScopeFactory scopeFactory,
    MigrationState state,
    IConfiguration configuration,
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
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync(stoppingToken);

                state.Completed = true;
                logger.LogInformation("Database migrations applied.");
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
}
