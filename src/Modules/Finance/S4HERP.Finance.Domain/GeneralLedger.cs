using System.ComponentModel.DataAnnotations;
using S4HERP.BuildingBlocks.Domain;
using S4HERP.Organization.Domain;

namespace S4HERP.Finance.Domain;

public enum LedgerType
{
    Leading = 1,
    NonLeading = 2,
}

public enum GLAccountType
{
    BalanceSheet = 1,
    ProfitAndLoss = 2,
}

/// <summary>
/// An accounting standard's book. The leading ledger carries the local statutory
/// close; non-leading ledgers carry IFRS or other parallel valuations.
/// </summary>
public class Ledger : AuditableEntity, IDeactivatable
{
    [MaxLength(4)] public required string Code { get; set; }
    [MaxLength(60)] public required string Name { get; set; }
    public LedgerType LedgerType { get; set; }
    [MaxLength(20)] public required string AccountingStandard { get; set; }
    public long? FiscalYearVariantId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ChartOfAccounts : AuditableEntity, IDeactivatable
{
    [MaxLength(4)] public required string Code { get; set; }
    [MaxLength(60)] public required string Name { get; set; }
    [MaxLength(2)] public required string MaintenanceLanguage { get; set; }
    public byte AccountNumberLength { get; set; } = 10;
    public bool IsActive { get; set; } = true;
}

public class GLAccountGroup : AuditableEntity
{
    [MaxLength(4)] public required string Code { get; set; }
    [MaxLength(60)] public required string Name { get; set; }
    public long ChartOfAccountsId { get; set; }
    public ChartOfAccounts ChartOfAccounts { get; set; } = null!;
    [MaxLength(10)] public required string FromAccount { get; set; }
    [MaxLength(10)] public required string ToAccount { get; set; }
}

/// <summary>Chart-of-accounts level account data, shared by every company code using the chart.</summary>
public class GLAccount : AuditableEntity, IDeactivatable
{
    [MaxLength(10)] public required string AccountNumber { get; set; }
    public long ChartOfAccountsId { get; set; }
    public ChartOfAccounts ChartOfAccounts { get; set; } = null!;
    public long GLAccountGroupId { get; set; }
    public GLAccountGroup GLAccountGroup { get; set; } = null!;

    [MaxLength(20)] public required string ShortText { get; set; }
    [MaxLength(200)] public required string LongText { get; set; }

    public GLAccountType AccountType { get; set; }

    /// <summary>
    /// Reconciliation accounts are posted to only through their subledger. A
    /// direct posting is rejected by the posting engine.
    /// </summary>
    public bool IsReconciliationAccount { get; set; }

    /// <summary>Account type the reconciliation account serves — D customer, K vendor, A asset.</summary>
    public AccountType? ReconciliationAccountType { get; set; }

    public bool IsRetainedEarningsAccount { get; set; }
    public bool IsBlocked { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Company-code level account settings. The same account behaves differently per entity.</summary>
public class GLAccountCompanyCode : AuditableEntity, IDeactivatable
{
    public long GLAccountId { get; set; }
    public GLAccount GLAccount { get; set; } = null!;
    public long CompanyCodeId { get; set; }
    public CompanyCode CompanyCode { get; set; } = null!;

    public long AccountCurrencyId { get; set; }

    public bool IsOpenItemManaged { get; set; }
    public bool IsLineItemDisplayed { get; set; } = true;
    public bool OnlyBalancesInLocalCurrency { get; set; }

    [MaxLength(4)] public string? FieldStatusGroup { get; set; }
    [MaxLength(2)] public string? DefaultTaxCode { get; set; }
    public bool IsTaxRelevant { get; set; }

    public bool IsPostingBlocked { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Required for P&L accounts when cost accounting is active.</summary>
    public bool RequiresCostObject { get; set; }
}

/// <summary>
/// Financial statement version — the hierarchy that turns account balances into
/// a balance sheet and P&amp;L.
/// </summary>
public class FinancialStatementVersion : AuditableEntity, IDeactivatable
{
    [MaxLength(4)] public required string Code { get; set; }
    [MaxLength(60)] public required string Name { get; set; }
    public long ChartOfAccountsId { get; set; }
    public ChartOfAccounts ChartOfAccounts { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}

public class FinancialStatementItem : AuditableEntity
{
    public long FinancialStatementVersionId { get; set; }
    public FinancialStatementVersion FinancialStatementVersion { get; set; } = null!;
    public long? ParentItemId { get; set; }
    public FinancialStatementItem? ParentItem { get; set; }

    [MaxLength(20)] public required string Code { get; set; }
    [MaxLength(120)] public required string Name { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>Inclusive account range assigned to this node. Null for pure headings.</summary>
    [MaxLength(10)] public string? FromAccount { get; set; }
    [MaxLength(10)] public string? ToAccount { get; set; }
}
