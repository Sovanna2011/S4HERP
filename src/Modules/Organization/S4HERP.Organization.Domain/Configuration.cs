using System.ComponentModel.DataAnnotations;
using S4HERP.BuildingBlocks.Domain;

namespace S4HERP.Organization.Domain;

/// <summary>Account type a posting line addresses. Stored as a single character.</summary>
public enum AccountType
{
    GeneralLedger = 'S',
    Customer = 'D',
    Vendor = 'K',
    Asset = 'A',
    Material = 'M',
}

public enum QuotationDirection
{
    Direct = 1,
    Indirect = 2,
}

public enum TaxDirection
{
    Input = 1,
    Output = 2,
}

public enum NumberRangeObject
{
    AccountingDocument = 1,
    BusinessPartner = 2,
    Asset = 3,
    ClearingDocument = 4,
    InternalOrder = 5,
}

public class Currency : AuditableEntity, IDeactivatable
{
    /// <summary>ISO 4217 alphabetic code.</summary>
    [MaxLength(3)] public required string Code { get; set; }
    [MaxLength(60)] public required string Name { get; set; }

    /// <summary>
    /// Display decimals per ISO 4217. Storage is always decimal(19,4) — this
    /// governs presentation and rounding, not precision.
    /// </summary>
    public byte DecimalPlaces { get; set; } = 2;

    public bool IsActive { get; set; } = true;
}

public class ExchangeRateType : AuditableEntity, IDeactivatable
{
    [MaxLength(4)] public required string Code { get; set; }
    [MaxLength(60)] public required string Name { get; set; }
    public QuotationDirection QuotationDirection { get; set; } = QuotationDirection.Direct;
    public bool IsActive { get; set; } = true;
}

public class ExchangeRate : AuditableEntity
{
    public long ExchangeRateTypeId { get; set; }
    public ExchangeRateType ExchangeRateType { get; set; } = null!;

    public long FromCurrencyId { get; set; }
    public long ToCurrencyId { get; set; }

    public DateOnly ValidFrom { get; set; }

    /// <summary>Units of the target currency per <see cref="FromRatio"/> units of the source.</summary>
    public decimal Rate { get; set; }

    public int FromRatio { get; set; } = 1;
    public int ToRatio { get; set; } = 1;
}

public class FiscalYearVariant : AuditableEntity
{
    [MaxLength(2)] public required string Code { get; set; }
    [MaxLength(60)] public required string Name { get; set; }
    public byte NormalPeriods { get; set; } = 12;
    public byte SpecialPeriods { get; set; } = 4;
    public bool IsCalendarYear { get; set; } = true;
    public bool IsYearDependent { get; set; }
}

/// <summary>One row per period of a fiscal year, giving its date boundaries.</summary>
public class FiscalYearPeriod : AuditableEntity
{
    public long FiscalYearVariantId { get; set; }
    public FiscalYearVariant FiscalYearVariant { get; set; } = null!;

    public short FiscalYear { get; set; }
    public byte Period { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsSpecialPeriod { get; set; }
}

public class PostingPeriodVariant : AuditableEntity
{
    [MaxLength(4)] public required string Code { get; set; }
    [MaxLength(60)] public required string Name { get; set; }
}

/// <summary>
/// Open/closed state per account type. Modelled per period rather than as a
/// range because closing is routinely partial — G/L open while AR and AP are shut.
/// </summary>
public class PostingPeriod : AuditableEntity
{
    public long PostingPeriodVariantId { get; set; }
    public PostingPeriodVariant PostingPeriodVariant { get; set; } = null!;

    public AccountType AccountType { get; set; }
    public short FiscalYear { get; set; }
    public byte Period { get; set; }
    public bool IsOpen { get; set; }

    /// <summary>When set, only holders of this authorisation group may post.</summary>
    [MaxLength(4)] public string? RestrictedToAuthorizationGroup { get; set; }
}

public class DocumentType : AuditableEntity, IDeactivatable
{
    [MaxLength(2)] public required string Code { get; set; }
    [MaxLength(60)] public required string Name { get; set; }

    [MaxLength(2)] public required string NumberRangeCode { get; set; }

    /// <summary>Account types this document type may post to, e.g. "SDK".</summary>
    [MaxLength(5)] public required string AllowedAccountTypes { get; set; }

    public bool IsReversalAllowed { get; set; } = true;
    [MaxLength(2)] public string? ReversalDocumentTypeCode { get; set; }
    public bool RequiresReference { get; set; }
    public bool IsIntercompany { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Concurrency-safe document number source. <see cref="IsGapless"/> selects
/// between a row lock held inside the posting transaction (gapless, serialised)
/// and a faster non-gapless allocation — ADR-11.
/// </summary>
public class NumberRange : AuditableEntity, IConcurrencyControlled
{
    [MaxLength(2)] public required string Code { get; set; }
    public NumberRangeObject ObjectType { get; set; }

    public long? CompanyCodeId { get; set; }
    public CompanyCode? CompanyCode { get; set; }

    public short FiscalYear { get; set; }

    public long FromNumber { get; set; }
    public long ToNumber { get; set; }
    public long CurrentNumber { get; set; }

    public bool IsGapless { get; set; } = true;
    public bool IsExternal { get; set; }

    [MaxLength(10)] public string? Prefix { get; set; }
    public byte PaddingLength { get; set; } = 10;
}

/// <summary>
/// A number allocated but never committed. Recorded so an auditor's question
/// about a missing document number has a documented answer.
/// </summary>
public class NumberRangeGap : AuditableEntity
{
    public long NumberRangeId { get; set; }
    public NumberRange NumberRange { get; set; } = null!;
    public long GapNumber { get; set; }
    public DateTime DetectedAtUtc { get; set; }
    [MaxLength(200)] public string? Reason { get; set; }
}

public class PostingKey : AuditableEntity
{
    [MaxLength(2)] public required string Code { get; set; }
    [MaxLength(60)] public required string Name { get; set; }
    public AccountType AccountType { get; set; }
    public bool IsDebit { get; set; }
    public bool IsSalesRelated { get; set; }
    public bool IsReversal { get; set; }
}

public class TaxCode : AuditableEntity, IDeactivatable, IValidityDated
{
    [MaxLength(2)] public required string Code { get; set; }
    [MaxLength(2)] public required string CountryCode { get; set; }
    [MaxLength(60)] public required string Name { get; set; }

    public TaxDirection Direction { get; set; }

    /// <summary>Percentage, e.g. 10.000000 for 10%.</summary>
    public decimal Rate { get; set; }

    public bool IsDeductible { get; set; } = true;

    /// <summary>G/L account the tax amount posts to.</summary>
    public long? TaxAccountId { get; set; }

    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; } = DateRange.OpenEnded;
    public bool IsActive { get; set; } = true;
}

public enum TransactionCodeTarget
{
    Ui5Route = 1,
    Report = 2,
    Job = 3,
    ExternalUrl = 4,
}

public class TransactionCode : AuditableEntity, IDeactivatable, IValidityDated
{
    [MaxLength(20)] public required string Code { get; set; }
    [MaxLength(120)] public required string Description { get; set; }
    [MaxLength(30)] public required string Category { get; set; }
    [MaxLength(30)] public required string Module { get; set; }

    public TransactionCodeTarget TargetType { get; set; } = TransactionCodeTarget.Ui5Route;
    [MaxLength(200)] public required string Target { get; set; }

    /// <summary>JSON object of preset route parameters, e.g. {"docType":"SA"}.</summary>
    [MaxLength(400)] public string? DefaultParameters { get; set; }

    [MaxLength(20)] public string? AuthorizationObjectCode { get; set; }
    [MaxLength(40)] public string? Icon { get; set; }
    [MaxLength(200)] public string? Keywords { get; set; }

    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; } = DateRange.OpenEnded;
    public bool IsActive { get; set; } = true;
}
