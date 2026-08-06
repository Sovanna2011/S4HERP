using System.ComponentModel.DataAnnotations;
using S4HERP.BuildingBlocks.Domain;
using S4HERP.Organization.Domain;

namespace S4HERP.BusinessPartner.Domain;

/// <summary>
/// Company-code level financial data. Created by the role synchroniser when
/// FI_CUST or FI_VEND is assigned, in the same transaction as the assignment.
/// </summary>
public class PartnerCompanyCode : AuditableEntity, IDeactivatable
{
    public long PartnerId { get; set; }
    public Partner Partner { get; set; } = null!;
    public long CompanyCodeId { get; set; }
    public CompanyCode CompanyCode { get; set; } = null!;

    /// <summary>
    /// The subledger account this partner's postings roll up to. Frozen once
    /// postings exist — a change requires an adjustment posting, not an edit.
    /// </summary>
    public long ReconciliationAccountId { get; set; }

    [MaxLength(4)] public string? PaymentTerms { get; set; }
    [MaxLength(1)] public string? PaymentMethods { get; set; }
    [MaxLength(4)] public string? DunningProcedure { get; set; }
    [MaxLength(4)] public string? ToleranceGroup { get; set; }
    [MaxLength(2)] public string? WithholdingTaxCode { get; set; }

    public bool IsPaymentBlocked { get; set; }
    public bool IsPostingBlocked { get; set; }
    public bool IsMarkedForDeletion { get; set; }

    [MaxLength(4)] public string? CorrespondenceType { get; set; }
    [MaxLength(64)] public string? Accountant { get; set; }

    public bool IsActive { get; set; } = true;
}

public class PartnerCustomer : AuditableEntity, IDeactivatable
{
    public long PartnerCompanyCodeId { get; set; }
    public PartnerCompanyCode PartnerCompanyCode { get; set; } = null!;

    [MaxLength(10)] public required string CustomerAccountNumber { get; set; }
    [MaxLength(4)] public required string AccountGroup { get; set; }

    public bool IsOneTimeAccount { get; set; }
    [MaxLength(4)] public string? HeadOfficeAccount { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PartnerVendor : AuditableEntity, IDeactivatable
{
    public long PartnerCompanyCodeId { get; set; }
    public PartnerCompanyCode PartnerCompanyCode { get; set; } = null!;

    [MaxLength(10)] public required string VendorAccountNumber { get; set; }
    [MaxLength(4)] public required string AccountGroup { get; set; }

    public bool IsOneTimeAccount { get; set; }
    public bool CheckDoubleInvoice { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class PartnerSalesArea : AuditableEntity, IDeactivatable
{
    public long PartnerId { get; set; }
    public Partner Partner { get; set; } = null!;
    public long SalesOrganizationId { get; set; }
    public SalesOrganization SalesOrganization { get; set; } = null!;

    [MaxLength(2)] public required string DistributionChannel { get; set; }
    [MaxLength(2)] public required string Division { get; set; }

    public long CurrencyId { get; set; }
    [MaxLength(4)] public string? PriceGroup { get; set; }
    [MaxLength(4)] public string? PriceList { get; set; }
    [MaxLength(3)] public string? Incoterms { get; set; }
    [MaxLength(4)] public string? ShippingConditions { get; set; }
    [MaxLength(2)] public string? CustomerGroup { get; set; }
    public bool IsDeliveryBlocked { get; set; }
    public bool IsBillingBlocked { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PartnerPurchasingOrganization : AuditableEntity, IDeactivatable
{
    public long PartnerId { get; set; }
    public Partner Partner { get; set; } = null!;
    public long PurchasingOrganizationId { get; set; }
    public PurchasingOrganization PurchasingOrganization { get; set; } = null!;

    [MaxLength(3)] public string? PurchasingGroup { get; set; }
    public long OrderCurrencyId { get; set; }
    [MaxLength(3)] public string? Incoterms { get; set; }
    [MaxLength(4)] public string? PaymentTerms { get; set; }
    public bool GoodsReceiptBasedInvoiceVerification { get; set; }
    public bool AutomaticPurchaseOrderAllowed { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Credit limit and exposure. Keyed by credit control area, not company code —
/// a per-company-code limit would silently multiply the customer's exposure.
/// </summary>
public class PartnerCreditProfile : AuditableEntity
{
    public long PartnerId { get; set; }
    public Partner Partner { get; set; } = null!;
    public long CreditControlAreaId { get; set; }
    public CreditControlArea CreditControlArea { get; set; } = null!;

    public decimal CreditLimit { get; set; }
    public long CurrencyId { get; set; }

    [MaxLength(3)] public string? RiskClass { get; set; }
    public DateOnly? NextReviewDate { get; set; }
    public bool IsBlocked { get; set; }
}
