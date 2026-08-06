using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace S4HERP.ArchitectureTests;

/// <summary>
/// The module boundaries from blueprint 02 are only real if something checks
/// them. These run in CI on every build.
/// </summary>
public class BoundaryTests
{
    private static readonly Assembly[] ModuleAssemblies =
    [
        typeof(Organization.Domain.Tenant).Assembly,
        typeof(Organization.Infrastructure.TenantConfiguration).Assembly,
        typeof(Security.Domain.User).Assembly,
        typeof(Security.Infrastructure.UserConfiguration).Assembly,
        typeof(Security.Application.AuthorizationEnforcer).Assembly,
        typeof(BusinessPartner.Domain.Partner).Assembly,
        typeof(BusinessPartner.Infrastructure.PartnerConfiguration).Assembly,
        typeof(Finance.Domain.JournalEntryLine).Assembly,
        typeof(Finance.Infrastructure.LedgerConfiguration).Assembly,
        typeof(Finance.Application.PostJournalEntryHandler).Assembly,
        typeof(Finance.Api.FinanceEndpoints).Assembly,
        typeof(Controlling.Domain.CostCenter).Assembly,
        typeof(Controlling.Infrastructure.ControllingAreaConfiguration).Assembly,
        typeof(Audit.Domain.AuditLog).Assembly,
        typeof(Audit.Infrastructure.AuditLogConfiguration).Assembly,
    ];

    /// <summary>
    /// ADR-17: Organization.Domain is the shared kernel for enterprise structure,
    /// and is the only module Domain another module may reference directly.
    /// </summary>
    [Fact]
    public void Modules_reference_only_the_shared_kernel_across_domains()
    {
        // BuildingBlocks carries mechanisms (Entity, ITenantScoped, DateRange) that
        // every module needs; Organization.Domain carries the enterprise structure
        // the whole ledger is expressed in (ADR-17). Everything else is off limits.
        string[] sharedKernels =
            ["S4HERP.Organization.Domain", "S4HERP.BuildingBlocks.Domain"];
        var violations = new List<string>();

        foreach (var assembly in ModuleAssemblies.Where(a => a.GetName().Name!.EndsWith(".Domain")))
        {
            var moduleName = ModuleOf(assembly);

            foreach (var reference in assembly.GetReferencedAssemblies()
                         .Select(r => r.Name!)
                         .Where(n => n.StartsWith("S4HERP.") && n.EndsWith(".Domain")))
            {
                if (reference == assembly.GetName().Name || sharedKernels.Contains(reference))
                {
                    continue;
                }

                if (ModuleOf(reference) != moduleName)
                {
                    violations.Add($"{assembly.GetName().Name} → {reference}");
                }
            }
        }

        Assert.True(violations.Count == 0,
            "Domain assemblies may reference only their own module and the shared kernel:\n"
            + string.Join('\n', violations));
    }

    /// <summary>A domain model that knows about EF Core is not a domain model.</summary>
    [Fact]
    public void Domain_assemblies_do_not_reference_persistence()
    {
        var violations = ModuleAssemblies
            .Where(a => a.GetName().Name!.EndsWith(".Domain"))
            .SelectMany(a => a.GetReferencedAssemblies()
                .Where(r => r.Name!.StartsWith("Microsoft.EntityFrameworkCore")
                            || r.Name!.StartsWith("Microsoft.AspNetCore"))
                .Select(r => $"{a.GetName().Name} → {r.Name}"))
            .ToList();

        Assert.True(violations.Count == 0,
            "Domain assemblies must not depend on EF Core or ASP.NET Core:\n"
            + string.Join('\n', violations));
    }

    /// <summary>
    /// ADR-02: a module writes only to its own schema. Checked by confirming that
    /// every entity configuration maps its entity into the schema its module owns.
    /// </summary>
    [Fact]
    public void Entity_configurations_map_into_their_own_module_schema()
    {
        var expected = new Dictionary<string, string[]>
        {
            ["Organization"] = ["org", "cfg"],
            ["Security"] = ["sec"],
            ["BusinessPartner"] = ["mdm"],
            // Finance also declares constraints on tables owned by Organization
            // and BusinessPartner, which is why those schemas appear here — see
            // CompanyCodeChartOfAccountsConfiguration.
            ["Finance"] = ["fin", "org", "mdm"],
            ["Controlling"] = ["co"],
            ["Audit"] = ["audit"],
        };

        var context = TestContext.Create();
        var violations = new List<string>();

        foreach (var entity in context.Model.GetEntityTypes())
        {
            var schema = entity.GetSchema();
            var module = ModuleOf(entity.ClrType.Assembly);
            if (schema is null || module is null || !expected.TryGetValue(module, out var allowed))
            {
                continue;
            }

            if (!allowed.Contains(schema))
            {
                violations.Add($"{entity.ClrType.Name} ({module}) → schema {schema}");
            }
        }

        Assert.True(violations.Count == 0,
            "Entities must live in a schema their module owns:\n" + string.Join('\n', violations));
    }

    /// <summary>
    /// Every mutable table carries a concurrency token, so a lost update is
    /// impossible rather than merely unlikely (§22.1). Posted financial documents
    /// are exempt: they have no update path at all (ADR-07).
    /// </summary>
    [Fact]
    public void Mutable_entities_carry_a_concurrency_token()
    {
        var immutableByDesign = new[]
        {
            nameof(Finance.Domain.JournalEntryLine),
            nameof(Audit.Domain.AuditLog),
            nameof(Audit.Domain.AuditLogField),
            nameof(Audit.Domain.TableBrowserLog),
            nameof(Finance.Domain.PostingIdempotency),
            nameof(Security.Domain.LoginHistory),
            nameof(Security.Domain.PasswordHistory),
        };

        var context = TestContext.Create();
        var violations = context.Model.GetEntityTypes()
            .Where(e => typeof(BuildingBlocks.Domain.IConcurrencyControlled)
                .IsAssignableFrom(e.ClrType))
            .Where(e => e.FindProperty("RowVersion")?.IsConcurrencyToken != true)
            .Select(e => e.ClrType.Name)
            .Except(immutableByDesign)
            .ToList();

        Assert.True(violations.Count == 0,
            "IConcurrencyControlled entities must map RowVersion as a concurrency token:\n"
            + string.Join('\n', violations));
    }

    /// <summary>
    /// ADR-03: every tenant-scoped entity must carry the global query filter, or
    /// one forgotten configuration becomes a cross-tenant read.
    /// </summary>
    [Fact]
    public void Tenant_scoped_entities_all_carry_the_query_filter()
    {
        var context = TestContext.Create();
        var violations = context.Model.GetEntityTypes()
            .Where(e => typeof(BuildingBlocks.Domain.ITenantScoped).IsAssignableFrom(e.ClrType))
            .Where(e => e.GetDeclaredQueryFilters().Count == 0)
            .Select(e => e.ClrType.Name)
            .ToList();

        Assert.True(violations.Count == 0,
            "Tenant-scoped entities without a query filter:\n" + string.Join('\n', violations));
    }

    private static string? ModuleOf(Assembly assembly) => ModuleOf(assembly.GetName().Name!);

    private static string? ModuleOf(string assemblyName)
    {
        var parts = assemblyName.Split('.');
        return parts.Length >= 3 && parts[0] == "S4HERP" ? parts[1] : null;
    }
}
