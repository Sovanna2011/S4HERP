using System.ComponentModel.DataAnnotations;
using S4HERP.BuildingBlocks.Domain;
using S4HERP.Organization.Domain;

namespace S4HERP.BusinessPartner.Domain;

public enum PartnerCategory
{
    Organization = 1,
    Person = 2,
    Group = 3,
}

public enum AddressType
{
    Registered = 1,
    Billing = 2,
    Shipping = 3,
    Correspondence = 4,
    Plant = 5,
}

public enum CommunicationType
{
    Phone = 1,
    Mobile = 2,
    Email = 3,
    Fax = 4,
    Website = 5,
}

public enum RelationshipType
{
    Parent = 1,
    Subsidiary = 2,
    ContactPerson = 3,
    Employee = 4,
    SoldTo = 5,
    ShipTo = 6,
    BillTo = 7,
    Payer = 8,
    Supplier = 9,
    RelatedCompany = 10,
    Intercompany = 11,
}

/// <summary>
/// The single identity. Customer and vendor are roles held by a partner, never
/// separate master records (ADR-10).
/// </summary>
public class Partner : AuditableEntity, IValidityDated, IDeactivatable
{
    /// <summary>Assigned from the BP group's number range.</summary>
    [MaxLength(10)] public required string PartnerNumber { get; set; }

    public PartnerCategory Category { get; set; }

    public long PartnerGroupId { get; set; }
    public PartnerGroup PartnerGroup { get; set; } = null!;

    [MaxLength(4)] public string? Title { get; set; }

    /// <summary>Organisation or group name. Null for a person.</summary>
    [MaxLength(200)] public string? Name { get; set; }

    [MaxLength(60)] public string? FirstName { get; set; }
    [MaxLength(60)] public string? LastName { get; set; }

    [MaxLength(40)] public string? SearchTerm1 { get; set; }
    [MaxLength(40)] public string? SearchTerm2 { get; set; }

    /// <summary>Normalised name used by the duplicate check.</summary>
    [MaxLength(200)] public string? NormalizedName { get; set; }

    [MaxLength(4)] public string? Industry { get; set; }
    [MaxLength(2)] public string? Language { get; set; }
    [MaxLength(2)] public string? CountryOfOrigin { get; set; }

    public bool IsBlocked { get; set; }
    public bool IsMarkedForDeletion { get; set; }

    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; } = DateRange.OpenEnded;
    public bool IsActive { get; set; } = true;

    public ICollection<PartnerRoleAssignment> RoleAssignments { get; set; } = [];
    public ICollection<PartnerAddress> Addresses { get; set; } = [];
}

/// <summary>Account group. Drives the number range and field status for a partner.</summary>
public class PartnerGroup : AuditableEntity
{
    [MaxLength(4)] public required string Code { get; set; }
    [MaxLength(60)] public required string Name { get; set; }
    public PartnerCategory Category { get; set; }
    [MaxLength(2)] public required string NumberRangeCode { get; set; }

    /// <summary>
    /// When true, the customer and vendor account numbers equal the partner
    /// number rather than being drawn from separate ranges.
    /// </summary>
    public bool UseSameNumber { get; set; } = true;
}

/// <summary>Catalogue of roles. Configuration, not master data.</summary>
public class PartnerRole : AuditableEntity
{
    [MaxLength(10)] public required string Code { get; set; }
    [MaxLength(60)] public required string Name { get; set; }

    /// <summary>Role that must already be held, e.g. FI_CUST requires CUST.</summary>
    [MaxLength(10)] public string? PrerequisiteRoleCode { get; set; }

    public bool RequiresCompanyCode { get; set; }
    public bool RequiresSalesArea { get; set; }
    public bool RequiresPurchasingOrganization { get; set; }

    /// <summary>Reconciliation account type the role needs — D customer, K vendor.</summary>
    public AccountType? ReconciliationAccountType { get; set; }
}

/// <summary>Which categories may hold which roles. Configurable because it varies by customer.</summary>
public class PartnerRoleCategoryCompatibility : AuditableEntity
{
    public long PartnerRoleId { get; set; }
    public PartnerRole PartnerRole { get; set; } = null!;
    public PartnerCategory Category { get; set; }
}

public class PartnerRoleAssignment : AuditableEntity, IValidityDated
{
    public long PartnerId { get; set; }
    public Partner Partner { get; set; } = null!;
    public long PartnerRoleId { get; set; }
    public PartnerRole PartnerRole { get; set; } = null!;
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; } = DateRange.OpenEnded;
}

public class PartnerAddress : AuditableEntity, IValidityDated
{
    public long PartnerId { get; set; }
    public Partner Partner { get; set; } = null!;
    public AddressType AddressType { get; set; }
    public bool IsDefault { get; set; }

    [MaxLength(200)] public required string Line1 { get; set; }
    [MaxLength(200)] public string? Line2 { get; set; }
    [MaxLength(100)] public string? District { get; set; }
    [MaxLength(100)] public required string City { get; set; }
    [MaxLength(100)] public string? Region { get; set; }
    [MaxLength(20)] public string? PostalCode { get; set; }
    [MaxLength(2)] public required string CountryCode { get; set; }

    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; } = DateRange.OpenEnded;

    public ICollection<PartnerCommunication> Communications { get; set; } = [];
}

/// <summary>
/// Attached to an address rather than to the partner: a head office and three
/// plants legitimately have different contact details.
/// </summary>
public class PartnerCommunication : AuditableEntity
{
    public long PartnerAddressId { get; set; }
    public PartnerAddress PartnerAddress { get; set; } = null!;
    public CommunicationType CommunicationType { get; set; }
    [MaxLength(200)] public required string Value { get; set; }
    public bool IsDefault { get; set; }
}

public class PartnerIdentification : AuditableEntity, IValidityDated
{
    public long PartnerId { get; set; }
    public Partner Partner { get; set; } = null!;
    [MaxLength(10)] public required string IdentificationType { get; set; }
    [MaxLength(60)] public required string IdentificationNumber { get; set; }
    [MaxLength(2)] public string? CountryCode { get; set; }
    [MaxLength(120)] public string? IssuingAuthority { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; } = DateRange.OpenEnded;
}

public class PartnerTaxNumber : AuditableEntity
{
    public long PartnerId { get; set; }
    public Partner Partner { get; set; } = null!;
    [MaxLength(2)] public required string CountryCode { get; set; }
    [MaxLength(10)] public required string TaxNumberType { get; set; }
    [MaxLength(30)] public required string TaxNumber { get; set; }
}

/// <summary>
/// Bank details. The highest-risk master data in the system — changes route
/// through maker-checker approval by default.
/// </summary>
public class PartnerBank : AuditableEntity, IValidityDated
{
    public long PartnerId { get; set; }
    public Partner Partner { get; set; } = null!;
    [MaxLength(2)] public required string CountryCode { get; set; }
    [MaxLength(15)] public required string BankKey { get; set; }
    [MaxLength(120)] public required string BankName { get; set; }
    [MaxLength(35)] public required string AccountNumber { get; set; }
    [MaxLength(60)] public required string AccountHolder { get; set; }
    [MaxLength(34)] public string? Iban { get; set; }
    [MaxLength(11)] public string? Swift { get; set; }
    public bool IsDefault { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; } = DateRange.OpenEnded;
}

public class PartnerRelationship : AuditableEntity, IValidityDated
{
    public long SourcePartnerId { get; set; }
    public Partner SourcePartner { get; set; } = null!;
    public long TargetPartnerId { get; set; }
    public Partner TargetPartner { get; set; } = null!;
    public RelationshipType RelationshipType { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; } = DateRange.OpenEnded;
}
