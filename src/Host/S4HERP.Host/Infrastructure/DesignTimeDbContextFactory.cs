using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using S4HERP.BuildingBlocks.Infrastructure;

namespace S4HERP.Host.Infrastructure;

/// <summary>
/// Lets <c>dotnet ef</c> build the context without booting the web host, so
/// migrations can be scaffolded with no database running.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<S4herpDbContext>
{
    public S4herpDbContext CreateDbContext(string[] args)
    {
        ModuleRegistration.UseModuleConfigurations();

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Server=localhost,1433;Database=S4HERP;User Id=sa;Password=DesignTimeOnly;"
               + "Encrypt=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<S4herpDbContext>()
            .UseSqlServer(connectionString, sql => sql
                .MigrationsAssembly(typeof(DesignTimeDbContextFactory).Assembly.FullName)
                .MigrationsHistoryTable("__EFMigrationsHistory", Schemas.Cfg))
            .Options;

        return new S4herpDbContext(
            options, FixedContext.System, FixedContext.System, new SystemClock());
    }
}
