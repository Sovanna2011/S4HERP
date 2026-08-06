using Microsoft.EntityFrameworkCore;
using S4HERP.BuildingBlocks.Infrastructure;

namespace S4HERP.Host.Infrastructure;

/// <summary>
/// Applies migrations and the post-migration scripts, then exits. Separated from
/// the web host so production applies the schema once, from a deployment job,
/// rather than from whichever replica happens to start first.
/// </summary>
public static class MigrateCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);
        builder.Services.AddSingleton<IClock, SystemClock>();
        builder.Services.AddSingleton<SystemDbContextFactory>();

        using var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("S4HERP.Migrate");
        var factory = host.Services.GetRequiredService<SystemDbContextFactory>();

        try
        {
            await using var db = factory.Create();

            var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
            logger.LogInformation("{Count} pending migration(s): {Migrations}",
                pending.Count, pending.Count == 0 ? "none" : string.Join(", ", pending));

            await db.Database.MigrateAsync();
            await PostMigrationScripts.ApplyAsync(db, logger, CancellationToken.None);

            logger.LogInformation("Schema is current.");
            return 0;
        }
        catch (Exception ex)
        {
            // A failed deployment must fail loudly; a non-zero exit is what stops
            // the rollout from proceeding to the application containers.
            logger.LogCritical(ex, "Schema setup failed.");
            return 1;
        }
    }
}
