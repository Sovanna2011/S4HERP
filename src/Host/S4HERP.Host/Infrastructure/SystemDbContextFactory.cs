using Microsoft.EntityFrameworkCore;
using S4HERP.BuildingBlocks.Infrastructure;

namespace S4HERP.Host.Infrastructure;

/// <summary>
/// Creates a context bound to the system principal, for the two operations that
/// are legitimately cross-tenant: schema migration and seeding, and resolving
/// which user — and therefore which tenant — a request belongs to.
///
/// Identity resolution has to sit outside tenant isolation by construction: the
/// tenant is not known until the user is. Doing it on the request-scoped context
/// silently fails instead, because that context opens its connection with an
/// unset tenant and row-level security then hides sec.User from it.
///
/// Elevation lives here, in one place, rather than as a settable flag on the
/// request context, so no request path can reach it.
/// </summary>
public sealed class SystemDbContextFactory(IConfiguration configuration, IClock clock)
{
    public S4herpDbContext Create()
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is missing.");

        var options = new DbContextOptionsBuilder<S4herpDbContext>()
            .AddInterceptors(new TenantSessionInterceptor(FixedContext.System))
            .UseSqlServer(connectionString, sql => sql
                .MigrationsAssembly(typeof(ModuleRegistration).Assembly.FullName)
                .MigrationsHistoryTable("__EFMigrationsHistory", Schemas.Cfg))
            .Options;

        return new S4herpDbContext(options, FixedContext.System, FixedContext.System, clock);
    }
}
