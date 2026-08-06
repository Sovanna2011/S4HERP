using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using S4HERP.BuildingBlocks.Infrastructure;
using S4HERP.Finance.Domain;
using S4HERP.Organization.Domain;
using S4HERP.Organization.Infrastructure;

namespace S4HERP.Finance.Infrastructure;

public sealed class DebitCreditConverter()
    : ValueConverter<DebitCredit, string>(
        v => ((char)v).ToString(),
        v => (DebitCredit)v[0]);

public class LedgerConfiguration : IEntityTypeConfiguration<Ledger>
{
    public void Configure(EntityTypeBuilder<Ledger> b)
    {
        b.ToTable("Ledger", Schemas.Fin);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();

        // Exactly one leading ledger per tenant.
        b.HasIndex(x => x.TenantId)
            .IsUnique()
            .HasFilter("[LedgerType] = 1")
            .HasDatabaseName("UX_Ledger_OneLeadingPerTenant");
    }
}

public class ChartOfAccountsConfiguration : IEntityTypeConfiguration<ChartOfAccounts>
{
    public void Configure(EntityTypeBuilder<ChartOfAccounts> b)
    {
        b.ToTable("ChartOfAccounts", Schemas.Fin);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

/// <summary>
/// Owns the CompanyCode → ChartOfAccounts constraint. Organization cannot declare
/// it without depending on Finance, which would create a module cycle.
/// </summary>
public class CompanyCodeChartOfAccountsConfiguration : IEntityTypeConfiguration<CompanyCode>
{
    public void Configure(EntityTypeBuilder<CompanyCode> b)
    {
        b.HasOne<ChartOfAccounts>().WithMany().HasForeignKey(x => x.ChartOfAccountsId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class GLAccountGroupConfiguration : IEntityTypeConfiguration<GLAccountGroup>
{
    public void Configure(EntityTypeBuilder<GLAccountGroup> b)
    {
        b.ToTable("GLAccountGroup", Schemas.Fin);
        b.HasIndex(x => new { x.TenantId, x.ChartOfAccountsId, x.Code }).IsUnique();
        b.HasOne(x => x.ChartOfAccounts).WithMany().HasForeignKey(x => x.ChartOfAccountsId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class GLAccountConfiguration : IEntityTypeConfiguration<GLAccount>
{
    public void Configure(EntityTypeBuilder<GLAccount> b)
    {
        b.ToTable("GLAccount", Schemas.Fin);
        b.Property(x => x.ReconciliationAccountType)
            .HasConversion<AccountTypeConverter>().HasMaxLength(1);

        b.HasIndex(x => new { x.TenantId, x.ChartOfAccountsId, x.AccountNumber }).IsUnique();

        b.HasOne(x => x.ChartOfAccounts).WithMany().HasForeignKey(x => x.ChartOfAccountsId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.GLAccountGroup).WithMany().HasForeignKey(x => x.GLAccountGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        // A reconciliation account must say which subledger it reconciles.
        b.ToTable(t => t.HasCheckConstraint(
            "CK_GLAccount_ReconciliationType",
            "([IsReconciliationAccount] = 0 AND [ReconciliationAccountType] IS NULL) OR " +
            "([IsReconciliationAccount] = 1 AND [ReconciliationAccountType] IS NOT NULL)"));
    }
}

public class GLAccountCompanyCodeConfiguration : IEntityTypeConfiguration<GLAccountCompanyCode>
{
    public void Configure(EntityTypeBuilder<GLAccountCompanyCode> b)
    {
        b.ToTable("GLAccountCompanyCode", Schemas.Fin);
        b.HasIndex(x => new { x.TenantId, x.GLAccountId, x.CompanyCodeId }).IsUnique();

        b.HasOne(x => x.GLAccount).WithMany().HasForeignKey(x => x.GLAccountId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.CompanyCode).WithMany().HasForeignKey(x => x.CompanyCodeId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Currency>().WithMany().HasForeignKey(x => x.AccountCurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FinancialStatementVersionConfiguration
    : IEntityTypeConfiguration<FinancialStatementVersion>
{
    public void Configure(EntityTypeBuilder<FinancialStatementVersion> b)
    {
        b.ToTable("FinancialStatementVersion", Schemas.Fin);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.HasOne(x => x.ChartOfAccounts).WithMany().HasForeignKey(x => x.ChartOfAccountsId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FinancialStatementItemConfiguration : IEntityTypeConfiguration<FinancialStatementItem>
{
    public void Configure(EntityTypeBuilder<FinancialStatementItem> b)
    {
        b.ToTable("FinancialStatementItem", Schemas.Fin);
        b.HasIndex(x => new { x.TenantId, x.FinancialStatementVersionId, x.Code }).IsUnique();

        b.HasOne(x => x.FinancialStatementVersion).WithMany()
            .HasForeignKey(x => x.FinancialStatementVersionId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.ParentItem).WithMany()
            .HasForeignKey(x => x.ParentItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Owns the BusinessPartnerCompanyCode → GLAccount reconciliation-account
/// constraint. Declared here because Finance owns fin.GLAccount; the module
/// dependency runs Finance → BusinessPartner, never the reverse.
/// </summary>
public class PartnerReconciliationAccountConfiguration
    : IEntityTypeConfiguration<S4HERP.BusinessPartner.Domain.PartnerCompanyCode>
{
    public void Configure(EntityTypeBuilder<S4HERP.BusinessPartner.Domain.PartnerCompanyCode> b)
    {
        b.HasOne<GLAccount>().WithMany().HasForeignKey(x => x.ReconciliationAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
