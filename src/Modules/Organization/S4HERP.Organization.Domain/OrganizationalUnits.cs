using System.ComponentModel.DataAnnotations;
using S4HERP.BuildingBlocks.Domain;

namespace S4HERP.Organization.Domain;

/// <summary>
/// The isolation boundary. Not itself tenant-scoped — this table defines the
/// tenants that every other table's <c>TenantId</c> refers to.
/// </summary>
public class Tenant : Entity, IAuditable, IConcurrencyControlled, IDeactivatable
{
    [MaxLength(10)] public required string Code { get; set; }
    [MaxLength(120)] public required string Name { get; set; }
    [MaxLength(2)] public required string DefaultLanguage { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }
    [MaxLength(64)] public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedAtUtc { get; set; }
    [MaxLength(64)] public string? ModifiedBy { get; set; }
    public byte[]? RowVersion { get; set; }
}

/// <summary>Consolidation unit. Groups the company codes of one legal group.</summary>
public class Company : AuditableEntity, IDeactivatable
{
    [MaxLength(6)] public required string Code { get; set; }
    [MaxLength(120)] public required string Name { get; set; }
    [MaxLength(2)] public required string CountryCode { get; set; }

    /// <summary>Currency the group consolidates in.</summary>
    public long GroupCurrencyId { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// The legal entity, and the primary posting dimension. Everything financial
/// belongs to exactly one company code.
/// </summary>
public class CompanyCode : AuditableEntity, IDeactivatable
{
    [MaxLength(4)] public required string Code { get; set; }
    [MaxLength(120)] public required string Name { get; set; }
    [MaxLength(2)] public required string CountryCode { get; set; }
    [MaxLength(2)] public required string Language { get; set; }

    public long CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    // Frozen once any document is posted — see StructuralChangeGuard (Phase 3).
    public long LocalCurrencyId { get; set; }
    public long ChartOfAccountsId { get; set; }
    public long FiscalYearVariantId { get; set; }
    public long PostingPeriodVariantId { get; set; }
    public long? FieldStatusVariantId { get; set; }
    public long? CreditControlAreaId { get; set; }

    [MaxLength(200)] public string? AddressLine1 { get; set; }
    [MaxLength(200)] public string? AddressLine2 { get; set; }
    [MaxLength(100)] public string? City { get; set; }
    [MaxLength(20)] public string? PostalCode { get; set; }
    [MaxLength(30)] public string? TaxNumber { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>Cross-company internal reporting dimension.</summary>
public class BusinessArea : AuditableEntity, IDeactivatable
{
    [MaxLength(4)] public required string Code { get; set; }
    [MaxLength(120)] public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>IFRS 8 reporting segment. Derivable from the profit centre.</summary>
public class Segment : AuditableEntity, IDeactivatable
{
    [MaxLength(10)] public required string Code { get; set; }
    [MaxLength(120)] public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Cost-of-sales reporting dimension.</summary>
public class FunctionalArea : AuditableEntity, IDeactivatable
{
    [MaxLength(4)] public required string Code { get; set; }
    [MaxLength(120)] public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Plant : AuditableEntity, IDeactivatable
{
    [MaxLength(4)] public required string Code { get; set; }
    [MaxLength(120)] public required string Name { get; set; }
    public long CompanyCodeId { get; set; }
    public CompanyCode CompanyCode { get; set; } = null!;
    [MaxLength(100)] public string? City { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Branch : AuditableEntity, IDeactivatable
{
    [MaxLength(6)] public required string Code { get; set; }
    [MaxLength(120)] public required string Name { get; set; }
    public long CompanyCodeId { get; set; }
    public CompanyCode CompanyCode { get; set; } = null!;
    [MaxLength(100)] public string? City { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SalesOrganization : AuditableEntity, IDeactivatable
{
    [MaxLength(4)] public required string Code { get; set; }
    [MaxLength(120)] public required string Name { get; set; }
    public long CompanyCodeId { get; set; }
    public CompanyCode CompanyCode { get; set; } = null!;
    public long CurrencyId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PurchasingOrganization : AuditableEntity, IDeactivatable
{
    [MaxLength(4)] public required string Code { get; set; }
    [MaxLength(120)] public required string Name { get; set; }
    public long? CompanyCodeId { get; set; }
    public CompanyCode? CompanyCode { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Credit limit boundary. Spans company codes deliberately — a limit that applies
/// per company code would multiply the customer's effective exposure.
/// </summary>
public class CreditControlArea : AuditableEntity, IDeactivatable
{
    [MaxLength(4)] public required string Code { get; set; }
    [MaxLength(120)] public required string Name { get; set; }
    public long CurrencyId { get; set; }
    public bool IsActive { get; set; } = true;
}
