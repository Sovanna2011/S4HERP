using System.ComponentModel.DataAnnotations;
using S4HERP.BuildingBlocks.Domain;

namespace S4HERP.Audit.Domain;

public enum AuditAction
{
    Create = 1,
    Change = 2,
    Delete = 3,
    Post = 4,
    Reverse = 5,
    Approve = 6,
    Reject = 7,
    Block = 8,
    Unblock = 9,
    Export = 10,
    Login = 11,
    AuthorizationChange = 12,
}

/// <summary>
/// Business audit history, kept separate from the technical Serilog stream
/// (§22.1). Append-only: records are never updated or deleted, and posted-document
/// entries are immutable by construction.
/// </summary>
public class AuditLog : Entity, ITenantScoped
{
    public long TenantId { get; set; }
    public DateTime OccurredAtUtc { get; set; }

    [MaxLength(64)] public required string UserName { get; set; }
    public long? CompanyCodeId { get; set; }

    public AuditAction Action { get; set; }

    /// <summary>Logical object type, e.g. BusinessPartner, JournalEntry, Role.</summary>
    [MaxLength(60)] public required string ObjectType { get; set; }

    /// <summary>Business key of the affected object, rendered as text.</summary>
    [MaxLength(120)] public required string ObjectId { get; set; }

    [MaxLength(60)] public string? SourceScreen { get; set; }
    [MaxLength(200)] public string? SourceApi { get; set; }
    [MaxLength(20)] public string? TransactionCode { get; set; }
    public Guid? CorrelationId { get; set; }
    [MaxLength(45)] public string? SourceIpAddress { get; set; }

    [MaxLength(400)] public string? Summary { get; set; }

    public ICollection<AuditLogField> Fields { get; set; } = [];
}

/// <summary>
/// Field-level before and after values for configuration and master-data changes.
/// Values are text so the table is queryable in SE16N without per-type handling.
/// </summary>
public class AuditLogField : Entity, ITenantScoped
{
    public long TenantId { get; set; }
    public long AuditLogId { get; set; }
    public AuditLog AuditLog { get; set; } = null!;

    [MaxLength(120)] public required string FieldName { get; set; }
    [MaxLength(1000)] public string? OldValue { get; set; }
    [MaxLength(1000)] public string? NewValue { get; set; }
}

/// <summary>
/// Every table browser query. Filter values are recorded because "who looked up
/// this partner's bank details, and when" is what an investigation asks.
/// </summary>
public class TableBrowserLog : Entity, ITenantScoped
{
    public long TenantId { get; set; }
    public DateTime ExecutedAtUtc { get; set; }
    [MaxLength(64)] public required string UserName { get; set; }

    [MaxLength(128)] public required string TableName { get; set; }
    [MaxLength(2000)] public string? SelectedFields { get; set; }
    [MaxLength(4000)] public string? FilterJson { get; set; }
    [MaxLength(400)] public string? SortJson { get; set; }

    public int RowsReturned { get; set; }
    public bool WasExported { get; set; }
    public int DurationMs { get; set; }
    public Guid? CorrelationId { get; set; }
    [MaxLength(45)] public string? SourceIpAddress { get; set; }
}
