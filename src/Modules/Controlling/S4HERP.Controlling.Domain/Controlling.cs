using System.ComponentModel.DataAnnotations;
using S4HERP.BuildingBlocks.Domain;
using S4HERP.Organization.Domain;

namespace S4HERP.Controlling.Domain;

public enum CostCenterCategory
{
    Administration = 1,
    Production = 2,
    Sales = 3,
    Service = 4,
    Development = 5,
    Logistics = 6,
}

public enum InternalOrderCategory
{
    /// <summary>Collects real cost and must be settled.</summary>
    Real = 1,

    /// <summary>Records cost for information alongside a real cost object; never settled.</summary>
    Statistical = 2,
}

public enum SettlementReceiverType
{
    CostCenter = 1,
    Asset = 2,
    GeneralLedger = 3,
    InternalOrder = 4,
}

public enum AllocationMethod
{
    Distribution = 1,
    Assessment = 2,
}

/// <summary>
/// Cost accounting boundary. May span company codes only when they share a chart
/// of accounts and fiscal year variant (rule R5).
/// </summary>
public class ControllingArea : AuditableEntity, IDeactivatable
{
    [MaxLength(4)] public required string Code { get; set; }
    [MaxLength(60)] public required string Name { get; set; }
    public long CurrencyId { get; set; }
    public long ChartOfAccountsId { get; set; }
    public long FiscalYearVariantId { get; set; }
    public bool CrossCompanyCodeCostAccounting { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Validity-dated assignment of a company code to a controlling area (rule R8).</summary>
public class ControllingAreaCompanyCode : AuditableEntity, IValidityDated
{
    public long ControllingAreaId { get; set; }
    public ControllingArea ControllingArea { get; set; } = null!;
    public long CompanyCodeId { get; set; }
    public CompanyCode CompanyCode { get; set; } = null!;
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; } = DateRange.OpenEnded;
}

public class CostCenter : AuditableEntity, IValidityDated, IDeactivatable
{
    [MaxLength(10)] public required string Code { get; set; }
    [MaxLength(60)] public required string Name { get; set; }
    [MaxLength(200)] public string? Description { get; set; }

    public long ControllingAreaId { get; set; }
    public ControllingArea ControllingArea { get; set; } = null!;
    public long CompanyCodeId { get; set; }
    public CompanyCode CompanyCode { get; set; } = null!;

    public CostCenterCategory Category { get; set; }
    public long? ProfitCenterId { get; set; }
    public ProfitCenter? ProfitCenter { get; set; }
    public long? FunctionalAreaId { get; set; }
    public long? ParentCostCenterId { get; set; }
    public CostCenter? ParentCostCenter { get; set; }

    [MaxLength(64)] public string? ResponsiblePerson { get; set; }
    public long CurrencyId { get; set; }

    public bool IsLockedForActualPosting { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; } = DateRange.OpenEnded;
    public bool IsActive { get; set; } = true;
}

public class ProfitCenter : AuditableEntity, IValidityDated, IDeactivatable
{
    [MaxLength(10)] public required string Code { get; set; }
    [MaxLength(60)] public required string Name { get; set; }
    [MaxLength(200)] public string? Description { get; set; }

    public long ControllingAreaId { get; set; }
    public ControllingArea ControllingArea { get; set; } = null!;

    /// <summary>Segment derived onto journal lines carrying this profit centre.</summary>
    public long? SegmentId { get; set; }

    public long? ParentProfitCenterId { get; set; }
    public ProfitCenter? ParentProfitCenter { get; set; }

    [MaxLength(64)] public string? ResponsiblePerson { get; set; }
    public bool IsLockedForActualPosting { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; } = DateRange.OpenEnded;
    public bool IsActive { get; set; } = true;
}

public class InternalOrderType : AuditableEntity
{
    [MaxLength(4)] public required string Code { get; set; }
    [MaxLength(60)] public required string Name { get; set; }
    public InternalOrderCategory Category { get; set; }
    [MaxLength(2)] public required string NumberRangeCode { get; set; }
    public bool BudgetControlEnabled { get; set; }
    public SettlementReceiverType DefaultSettlementReceiver { get; set; }
}

public class InternalOrder : AuditableEntity, IDeactivatable
{
    [MaxLength(12)] public required string Code { get; set; }
    [MaxLength(60)] public required string Name { get; set; }

    public long InternalOrderTypeId { get; set; }
    public InternalOrderType InternalOrderType { get; set; } = null!;
    public long ControllingAreaId { get; set; }
    public ControllingArea ControllingArea { get; set; } = null!;
    public long CompanyCodeId { get; set; }
    public CompanyCode CompanyCode { get; set; } = null!;

    public long ResponsibleCostCenterId { get; set; }
    public CostCenter ResponsibleCostCenter { get; set; } = null!;
    public long? ProfitCenterId { get; set; }

    public long CurrencyId { get; set; }
    public decimal? Budget { get; set; }

    /// <summary>Percentage of budget at which availability control warns, then blocks.</summary>
    public decimal? WarningTolerancePercent { get; set; }
    public decimal? BlockingTolerancePercent { get; set; }

    public bool IsClosed { get; set; }
    public DateOnly? ClosedOn { get; set; }
    public bool IsActive { get; set; } = true;
}

public class InternalOrderSettlementRule : AuditableEntity, IValidityDated
{
    public long InternalOrderId { get; set; }
    public InternalOrder InternalOrder { get; set; } = null!;

    public SettlementReceiverType ReceiverType { get; set; }
    public long ReceiverId { get; set; }

    /// <summary>Share of the collected cost, 0–100. Rules for an order must total 100.</summary>
    public decimal SharePercent { get; set; }

    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; } = DateRange.OpenEnded;
}

/// <summary>Distribution or assessment cycle. Neither posts to the general ledger.</summary>
public class AllocationCycle : AuditableEntity, IValidityDated, IDeactivatable
{
    [MaxLength(10)] public required string Code { get; set; }
    [MaxLength(60)] public required string Name { get; set; }
    public long ControllingAreaId { get; set; }
    public ControllingArea ControllingArea { get; set; } = null!;
    public AllocationMethod Method { get; set; }

    /// <summary>Secondary cost element used by assessment. Null for distribution.</summary>
    public long? AssessmentCostElementId { get; set; }

    public bool IsIterative { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; } = DateRange.OpenEnded;
    public bool IsActive { get; set; } = true;
}

public class AllocationCycleSegment : AuditableEntity
{
    public long AllocationCycleId { get; set; }
    public AllocationCycle AllocationCycle { get; set; } = null!;
    public int SegmentNumber { get; set; }
    [MaxLength(60)] public required string Name { get; set; }

    public long SenderCostCenterId { get; set; }
    public long? ReceiverCostCenterId { get; set; }
    public long? ReceiverInternalOrderId { get; set; }

    /// <summary>Percentage, fixed portion, or the statistical key figure driving the split.</summary>
    public decimal? FixedPercent { get; set; }
    public decimal? FixedPortion { get; set; }
    public long? StatisticalKeyFigureId { get; set; }
}

public class StatisticalKeyFigure : AuditableEntity
{
    [MaxLength(10)] public required string Code { get; set; }
    [MaxLength(60)] public required string Name { get; set; }
    public long ControllingAreaId { get; set; }
    public ControllingArea ControllingArea { get; set; } = null!;
    [MaxLength(3)] public required string UnitOfMeasure { get; set; }

    /// <summary>True when the value is re-entered each period rather than carried forward.</summary>
    public bool IsTotalsValue { get; set; }
}
