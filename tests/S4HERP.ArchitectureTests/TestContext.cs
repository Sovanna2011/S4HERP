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
        S4herpDbContext.ConfigurationAssemblies.Clear();
        S4herpDbContext.ConfigurationAssemblies.AddRange(
        [
            typeof(Organization.Infrastructure.TenantConfiguration).Assembly,
            typeof(Security.Infrastructure.UserConfiguration).Assembly,
            typeof(BusinessPartner.Infrastructure.PartnerConfiguration).Assembly,
            typeof(Finance.Infrastructure.LedgerConfiguration).Assembly,
            typeof(Controlling.Infrastructure.ControllingAreaConfiguration).Assembly,
            typeof(Audit.Infrastructure.AuditLogConfiguration).Assembly,
        ]);

        var options = new DbContextOptionsBuilder<S4herpDbContext>()
            .UseSqlServer("Server=model-only;Database=S4HERP;Trusted_Connection=False")
            .Options;

        return new S4herpDbContext(
            options, FixedContext.System, FixedContext.System, new SystemClock());
    }
}
