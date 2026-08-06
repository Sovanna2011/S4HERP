using Microsoft.EntityFrameworkCore;
using S4HERP.BuildingBlocks.Infrastructure;

namespace S4HERP.Host.Infrastructure;

public static class ModuleRegistration
{
    /// <summary>
    /// The composition root. Every module contributes its entity configurations
    /// here and nowhere else, so adding or removing one is a single edit.
    /// </summary>
    public static IServiceCollection AddS4herpModules(
        this IServiceCollection services, string connectionString)
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

        services.AddSingleton<IClock, SystemClock>();

        // Phase 2 has no authentication yet, so the request context is the system
        // principal. Phase 3 replaces these with scoped, claims-derived contexts.
        services.AddScoped<ITenantContext>(_ => FixedContext.System);
        services.AddScoped<IUserContext>(_ => FixedContext.System);

        services.AddScoped<TenantSessionInterceptor>();

        services.AddDbContext<S4herpDbContext>((provider, options) => options
            .AddInterceptors(provider.GetRequiredService<TenantSessionInterceptor>())
            .UseSqlServer(connectionString, sql => sql
                .EnableRetryOnFailure()
                .MigrationsAssembly(typeof(ModuleRegistration).Assembly.FullName)
                .MigrationsHistoryTable("__EFMigrationsHistory", Schemas.Cfg)));

        return services;
    }
}
