using Microsoft.EntityFrameworkCore;
using S4HERP.BuildingBlocks.Infrastructure;

namespace S4HERP.ArchitectureTests;

/// <summary>
/// Builds the real model without a database. The architecture rules are
/// properties of the model, so they can be asserted at build time.
/// </summary>
internal static class TestContext
{
    public static S4herpDbContext Create()
    {
        // The host's list, not a copy of it: a model the tests build differently
        // from the one the application builds proves nothing about the application.
        S4HERP.Host.Infrastructure.ModuleRegistration.UseModuleConfigurations();

        var options = new DbContextOptionsBuilder<S4herpDbContext>()
            .UseSqlServer("Server=model-only;Database=S4HERP;Trusted_Connection=False")
            .Options;

        return new S4herpDbContext(
            options, FixedContext.System, FixedContext.System, new SystemClock());
    }
}
