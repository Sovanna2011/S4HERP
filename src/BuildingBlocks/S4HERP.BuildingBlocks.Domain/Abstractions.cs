namespace S4HERP.BuildingBlocks.Domain;

/// <summary>Root of every persisted domain object.</summary>
public abstract class DomainEntity;

/// <summary>An entity identified by a single surrogate key.</summary>
public abstract class Entity : DomainEntity
{
    public long Id { get; set; }
}

/// <summary>
/// Carried by every tenant-scoped row. The value is applied by the DbContext on
/// save and enforced again by SQL Server row-level security, so application code
/// never sets it directly.
/// </summary>
public interface ITenantScoped
{
    long TenantId { get; set; }
}

/// <summary>Who created and last changed the row, and when (UTC).</summary>
public interface IAuditable
{
    DateTime CreatedAtUtc { get; set; }
    string CreatedBy { get; set; }
    DateTime? ModifiedAtUtc { get; set; }
    string? ModifiedBy { get; set; }
}

/// <summary>Optimistic concurrency token. Absent from posted financial documents.</summary>
public interface IConcurrencyControlled
{
    byte[]? RowVersion { get; set; }
}

/// <summary>Master and configuration data that is deactivated rather than deleted.</summary>
public interface IDeactivatable
{
    bool IsActive { get; set; }
}

/// <summary>
/// Validity-dated record. <see cref="ValidTo"/> is exclusive and open-ended
/// validity is stored as <see cref="DateRange.OpenEnded"/> rather than null, so
/// range predicates need no null handling.
/// </summary>
public interface IValidityDated
{
    DateOnly ValidFrom { get; set; }
    DateOnly ValidTo { get; set; }
}

/// <summary>Convenience base for mutable master and configuration data.</summary>
public abstract class AuditableEntity : Entity, ITenantScoped, IAuditable, IConcurrencyControlled
{
    public long TenantId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }
    public byte[]? RowVersion { get; set; }
}
