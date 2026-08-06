using S4HERP.BuildingBlocks.Infrastructure;

namespace S4HERP.Host.Infrastructure;

public partial class SampleDataSeeder(S4herpDbContext db, ILogger logger)
{
    public Task SeedAsync(CancellationToken cancellationToken) => SeedCoreAsync(cancellationToken);
}
