namespace S4HERP.BuildingBlocks.Infrastructure;

/// <summary>The tenant every query and write in the current scope belongs to.</summary>
public interface ITenantContext
{
    long TenantId { get; }

    /// <summary>
    /// True only for migration, seeding and cross-tenant administration. Bypasses
    /// the EF global query filter; SQL Server row-level security still applies
    /// unless the connection is opened with the administrative session context.
    /// </summary>
    bool IsCrossTenant { get; }
}

/// <summary>Who is acting, for audit columns.</summary>
public interface IUserContext
{
    string UserName { get; }
}

/// <summary>Wall clock, injectable so tests are deterministic.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>Fixed context used by migrations, the seeder and background jobs.</summary>
public sealed class FixedContext(long tenantId, string userName, bool isCrossTenant = false)
    : ITenantContext, IUserContext
{
    public long TenantId { get; } = tenantId;
    public bool IsCrossTenant { get; } = isCrossTenant;
    public string UserName { get; } = userName;

    public static FixedContext System { get; } = new(0, "SYSTEM", isCrossTenant: true);
}
