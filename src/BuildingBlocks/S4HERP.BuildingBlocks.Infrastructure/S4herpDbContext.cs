using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using S4HERP.BuildingBlocks.Domain;

namespace S4HERP.BuildingBlocks.Infrastructure;

/// <summary>
/// The single context for the whole database.
///
/// ADR-16: one <see cref="DbContext"/> rather than one per module. Module
/// boundaries are kept by each module owning its schema and supplying its own
/// <see cref="IEntityTypeConfiguration{TEntity}"/> types; a single context is
/// what makes the atomic cross-module posting transaction required by ADR-01
/// straightforward rather than a distributed-transaction problem.
/// </summary>
public class S4herpDbContext(
    DbContextOptions<S4herpDbContext> options,
    ITenantContext tenantContext,
    IUserContext userContext,
    IClock clock) : DbContext(options)
{
    /// <summary>Assemblies scanned for entity configurations. Modules add themselves here.</summary>
    public static readonly List<Assembly> ConfigurationAssemblies = [];

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Financial amounts. Quantities and rates override this to (23,6).
        builder.Properties<decimal>().HavePrecision(19, 4);
        builder.Properties<DateTime>().HaveColumnType("datetime2(7)");
        builder.Properties<DateOnly>().HaveColumnType("date");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var assembly in ConfigurationAssemblies)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }

        ApplyCrossCuttingConventions(modelBuilder);
        ApplyTenantFilter(modelBuilder);
        AssertNoUnboundedStrings(modelBuilder);
    }

    private static void ApplyCrossCuttingConventions(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var clr = entity.ClrType;

            if (typeof(IConcurrencyControlled).IsAssignableFrom(clr))
            {
                modelBuilder.Entity(clr)
                    .Property(nameof(IConcurrencyControlled.RowVersion))
                    .IsRowVersion();
            }

            if (typeof(IAuditable).IsAssignableFrom(clr))
            {
                entity.FindProperty(nameof(IAuditable.CreatedBy))!.SetMaxLength(64);
                entity.FindProperty(nameof(IAuditable.ModifiedBy))!.SetMaxLength(64);
            }

            if (typeof(IDeactivatable).IsAssignableFrom(clr))
            {
                entity.FindProperty(nameof(IDeactivatable.IsActive))!.SetDefaultValue(true);
            }

            if (typeof(IValidityDated).IsAssignableFrom(clr))
            {
                entity.FindProperty(nameof(IValidityDated.ValidTo))!
                    .SetDefaultValue(DateRange.OpenEnded);
            }
        }
    }

    private void ApplyTenantFilter(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes()
                     .Where(e => typeof(ITenantScoped).IsAssignableFrom(e.ClrType)))
        {
            var parameter = Expression.Parameter(entity.ClrType, "e");
            var tenantId = Expression.Property(parameter, nameof(ITenantScoped.TenantId));

            // Captures `this`, so the filter reads the scope's tenant at query time.
            Expression<Func<long>> current = () => tenantContext.TenantId;
            Expression<Func<bool>> crossTenant = () => tenantContext.IsCrossTenant;

            var body = Expression.OrElse(
                Expression.Invoke(crossTenant),
                Expression.Equal(tenantId, Expression.Invoke(current)));

            modelBuilder.Entity(entity.ClrType)
                .HasQueryFilter(Expression.Lambda(body, parameter));
        }
    }

    /// <summary>
    /// Fails the model build if any string maps to nvarchar(max). Unbounded text
    /// silently ruins index and row-size planning on the journal tables, so long
    /// text must be opted into explicitly with HasColumnType("nvarchar(max)").
    /// </summary>
    private static void AssertNoUnboundedStrings(ModelBuilder modelBuilder)
    {
        var offenders = modelBuilder.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties()
                .Where(p => p.ClrType == typeof(string)
                            && p.GetMaxLength() is null
                            && p.GetColumnType() is null)
                .Select(p => $"{e.ClrType.Name}.{p.Name}"))
            .ToList();

        if (offenders.Count > 0)
        {
            throw new InvalidOperationException(
                "String properties without a maximum length: " + string.Join(", ", offenders) +
                ". Add [MaxLength] or set an explicit column type.");
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampEntries();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampEntries();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void StampEntries()
    {
        var now = clock.UtcNow;
        var user = userContext.UserName;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Added && entry.Entity is ITenantScoped scoped
                && scoped.TenantId == 0 && !tenantContext.IsCrossTenant)
            {
                scoped.TenantId = tenantContext.TenantId;
            }

            if (entry.Entity is not IAuditable auditable)
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    auditable.CreatedAtUtc = now;
                    if (string.IsNullOrEmpty(auditable.CreatedBy))
                    {
                        auditable.CreatedBy = user;
                    }

                    break;
                case EntityState.Modified:
                    auditable.ModifiedAtUtc = now;
                    auditable.ModifiedBy = user;
                    entry.Property(nameof(IAuditable.CreatedAtUtc)).IsModified = false;
                    entry.Property(nameof(IAuditable.CreatedBy)).IsModified = false;
                    break;
            }
        }
    }
}
