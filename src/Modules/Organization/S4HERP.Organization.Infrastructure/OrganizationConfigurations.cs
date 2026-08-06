using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using S4HERP.BuildingBlocks.Infrastructure;
using S4HERP.Organization.Domain;

namespace S4HERP.Organization.Infrastructure;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("Tenant", Schemas.Org);
        b.HasIndex(x => x.Code).IsUnique();
    }
}

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> b)
    {
        b.ToTable("Company", Schemas.Org);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.HasOne<Currency>().WithMany().HasForeignKey(x => x.GroupCurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CompanyCodeConfiguration : IEntityTypeConfiguration<CompanyCode>
{
    public void Configure(EntityTypeBuilder<CompanyCode> b)
    {
        b.ToTable("CompanyCode", Schemas.Org);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();

        b.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Currency>().WithMany().HasForeignKey(x => x.LocalCurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FiscalYearVariant>().WithMany().HasForeignKey(x => x.FiscalYearVariantId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PostingPeriodVariant>().WithMany().HasForeignKey(x => x.PostingPeriodVariantId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CreditControlArea>().WithMany().HasForeignKey(x => x.CreditControlAreaId)
            .OnDelete(DeleteBehavior.Restrict);

        // ChartOfAccountsId is constrained from the Finance module, which owns
        // the target table. Organization must not depend on Finance.
    }
}

public class BusinessAreaConfiguration : IEntityTypeConfiguration<BusinessArea>
{
    public void Configure(EntityTypeBuilder<BusinessArea> b)
    {
        b.ToTable("BusinessArea", Schemas.Org);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

public class SegmentConfiguration : IEntityTypeConfiguration<Segment>
{
    public void Configure(EntityTypeBuilder<Segment> b)
    {
        b.ToTable("Segment", Schemas.Org);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

public class FunctionalAreaConfiguration : IEntityTypeConfiguration<FunctionalArea>
{
    public void Configure(EntityTypeBuilder<FunctionalArea> b)
    {
        b.ToTable("FunctionalArea", Schemas.Org);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

public class PlantConfiguration : IEntityTypeConfiguration<Plant>
{
    public void Configure(EntityTypeBuilder<Plant> b)
    {
        b.ToTable("Plant", Schemas.Org);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.HasOne(x => x.CompanyCode).WithMany().HasForeignKey(x => x.CompanyCodeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> b)
    {
        b.ToTable("Branch", Schemas.Org);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.HasOne(x => x.CompanyCode).WithMany().HasForeignKey(x => x.CompanyCodeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SalesOrganizationConfiguration : IEntityTypeConfiguration<SalesOrganization>
{
    public void Configure(EntityTypeBuilder<SalesOrganization> b)
    {
        b.ToTable("SalesOrganization", Schemas.Org);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.HasOne(x => x.CompanyCode).WithMany().HasForeignKey(x => x.CompanyCodeId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Currency>().WithMany().HasForeignKey(x => x.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchasingOrganizationConfiguration : IEntityTypeConfiguration<PurchasingOrganization>
{
    public void Configure(EntityTypeBuilder<PurchasingOrganization> b)
    {
        b.ToTable("PurchasingOrganization", Schemas.Org);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.HasOne(x => x.CompanyCode).WithMany().HasForeignKey(x => x.CompanyCodeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CreditControlAreaConfiguration : IEntityTypeConfiguration<CreditControlArea>
{
    public void Configure(EntityTypeBuilder<CreditControlArea> b)
    {
        b.ToTable("CreditControlArea", Schemas.Org);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.HasOne<Currency>().WithMany().HasForeignKey(x => x.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
