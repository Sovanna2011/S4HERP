using Microsoft.EntityFrameworkCore;
using S4HERP.BuildingBlocks.Infrastructure;

namespace S4HERP.Host.Infrastructure;

public partial class SampleDataSeeder(S4herpDbContext db, ILogger logger)
{
    /// <summary>
    /// Seeds in one transaction. Without it a failure part-way leaves the tenant
    /// row behind, and the next start sees it and reports "already seeded" over a
    /// half-populated database — which is worse than failing.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await SeedCoreAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
}
