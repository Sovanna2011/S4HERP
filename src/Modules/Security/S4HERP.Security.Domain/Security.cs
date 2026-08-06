using System.ComponentModel.DataAnnotations;
using S4HERP.BuildingBlocks.Domain;

namespace S4HERP.Security.Domain;

public enum UserType
{
    Dialog = 1,
    Service = 2,
    Integration = 3,
    Api = 4,
    Background = 5,

    /// <summary>
    /// Display-only at the type level, not merely by role assignment, so granting
    /// an auditor access is a safe action rather than a careful one.
    /// </summary>
    Auditor = 6,
}

public enum SodSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
}

public class User : AuditableEntity, IValidityDated, IDeactivatable
{
    [MaxLength(64)] public required string UserName { get; set; }
    [MaxLength(120)] public required string DisplayName { get; set; }
    [MaxLength(256)] public required string Email { get; set; }

    [MaxLength(20)] public string? EmployeeNumber { get; set; }

    /// <summary>Business partner holding the EMPL role, when the user is an employee.</summary>
    public long? BusinessPartnerId { get; set; }

    public UserType UserType { get; set; } = UserType.Dialog;

    [MaxLength(2)] public required string Language { get; set; }
    [MaxLength(60)] public required string TimeZone { get; set; }
    public long? DefaultCompanyCodeId { get; set; }

    [MaxLength(256)] public string? PasswordHash { get; set; }

    /// <summary>
    /// Federated identity. An interactive user authenticated by an external
    /// provider legitimately has no local password hash, which is why the
    /// credential check accepts either.
    /// </summary>
    [MaxLength(60)] public string? ExternalIdentityProvider { get; set; }
    [MaxLength(200)] public string? ExternalSubjectId { get; set; }

    public DateTime? PasswordChangedAtUtc { get; set; }
    public bool MustChangePassword { get; set; }
    public bool MfaEnabled { get; set; }

    public bool IsLocked { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }

    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; } = DateRange.OpenEnded;
    public bool IsActive { get; set; } = true;
}

public class Role : AuditableEntity, IDeactivatable
{
    [MaxLength(40)] public required string Code { get; set; }
    [MaxLength(120)] public required string Name { get; set; }
    [MaxLength(400)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Permission : AuditableEntity
{
    [MaxLength(80)] public required string Code { get; set; }
    [MaxLength(120)] public required string Name { get; set; }
    [MaxLength(30)] public required string Module { get; set; }
}

public class UserRole : AuditableEntity, IValidityDated
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public long RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; } = DateRange.OpenEnded;
}

public class RolePermission : AuditableEntity
{
    public long RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public long PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}

/// <summary>Company codes a user may act in. Applied as a query predicate, never as a post-filter.</summary>
public class UserCompanyCode : AuditableEntity
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public long CompanyCodeId { get; set; }
}

/// <summary>
/// A value-carrying authorisation, e.g. F_BKPF_BUK with fields BUKRS and ACTVT.
/// Roles alone cannot express "post in company code 1000 up to 50,000 USD".
/// </summary>
public class AuthorizationObject : AuditableEntity
{
    [MaxLength(20)] public required string Code { get; set; }
    [MaxLength(120)] public required string Name { get; set; }
    [MaxLength(30)] public required string Module { get; set; }
}

public class AuthorizationField : AuditableEntity
{
    public long AuthorizationObjectId { get; set; }
    public AuthorizationObject AuthorizationObject { get; set; } = null!;
    [MaxLength(20)] public required string Code { get; set; }
    [MaxLength(120)] public required string Name { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>An instance of an authorisation object granted to a role.</summary>
public class RoleAuthorization : AuditableEntity
{
    public long RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public long AuthorizationObjectId { get; set; }
    public AuthorizationObject AuthorizationObject { get; set; } = null!;
    public ICollection<RoleAuthorizationValue> Values { get; set; } = [];
}

/// <summary>
/// One permitted value, range or wildcard for a field of a granted authorisation.
/// </summary>
public class RoleAuthorizationValue : AuditableEntity
{
    public long RoleAuthorizationId { get; set; }
    public RoleAuthorization RoleAuthorization { get; set; } = null!;
    public long AuthorizationFieldId { get; set; }
    public AuthorizationField AuthorizationField { get; set; } = null!;

    [MaxLength(60)] public required string FromValue { get; set; }
    [MaxLength(60)] public string? ToValue { get; set; }

    /// <summary>Full authority for the field. Recorded explicitly so wildcards are auditable.</summary>
    public bool IsWildcard { get; set; }
}

public class RoleTransactionCode : AuditableEntity
{
    public long RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public long TransactionCodeId { get; set; }
}

/// <summary>
/// Delegation of authority for a date range. A first-class object because the
/// alternative in practice is password sharing, which makes the audit trail fiction.
/// </summary>
public class UserSubstitution : AuditableEntity, IValidityDated
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public long SubstituteUserId { get; set; }
    public User SubstituteUser { get; set; } = null!;
    [MaxLength(200)] public string? Reason { get; set; }
    public bool IsActive { get; set; } = true;
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; } = DateRange.OpenEnded;
}

public class SegregationOfDutiesRule : AuditableEntity, IDeactivatable
{
    [MaxLength(40)] public required string Code { get; set; }
    [MaxLength(200)] public required string Name { get; set; }
    [MaxLength(400)] public required string Rationale { get; set; }
    public SodSeverity Severity { get; set; }

    [MaxLength(20)] public required string ConflictingAuthorizationObjectA { get; set; }
    [MaxLength(20)] public required string ConflictingAuthorizationObjectB { get; set; }

    public bool IsActive { get; set; } = true;
}

public class LoginHistory : Entity, ITenantScoped
{
    public long TenantId { get; set; }
    public long? UserId { get; set; }
    [MaxLength(64)] public required string AttemptedUserName { get; set; }
    public DateTime AttemptedAtUtc { get; set; }
    public bool Succeeded { get; set; }
    [MaxLength(60)] public string? FailureReason { get; set; }
    [MaxLength(45)] public string? SourceIpAddress { get; set; }
    [MaxLength(400)] public string? UserAgent { get; set; }
}

public class UserSession : Entity, ITenantScoped
{
    public long TenantId { get; set; }
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid SessionId { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    [MaxLength(45)] public string? SourceIpAddress { get; set; }
}

public class PasswordHistory : Entity, ITenantScoped
{
    public long TenantId { get; set; }
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    [MaxLength(256)] public required string PasswordHash { get; set; }
    public DateTime ChangedAtUtc { get; set; }
}
