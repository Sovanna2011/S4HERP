using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using S4HERP.BuildingBlocks.Domain;
using S4HERP.BuildingBlocks.Infrastructure;
using S4HERP.BusinessPartner.Domain;
using S4HERP.Organization.Domain;
using S4HERP.Organization.Infrastructure;

namespace S4HERP.BusinessPartner.Infrastructure;

public class PartnerConfiguration : IEntityTypeConfiguration<Partner>
{
    public void Configure(EntityTypeBuilder<Partner> b)
    {
        b.ToTable("BusinessPartner", Schemas.Mdm);
        b.HasIndex(x => new { x.TenantId, x.PartnerNumber }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.SearchTerm1 });
        b.HasIndex(x => new { x.TenantId, x.NormalizedName });

        b.HasOne(x => x.PartnerGroup).WithMany().HasForeignKey(x => x.PartnerGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        // A person carries first/last name; an organisation or group carries Name.
        b.ToTable(t => t.HasCheckConstraint(
            "CK_BusinessPartner_Name",
            "([Category] = 2 AND [LastName] IS NOT NULL) OR ([Category] IN (1,3) AND [Name] IS NOT NULL)"));
    }
}

public class PartnerGroupConfiguration : IEntityTypeConfiguration<PartnerGroup>
{
    public void Configure(EntityTypeBuilder<PartnerGroup> b)
    {
        b.ToTable("BusinessPartnerGroup", Schemas.Mdm);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

public class PartnerRoleConfiguration : IEntityTypeConfiguration<PartnerRole>
{
    public void Configure(EntityTypeBuilder<PartnerRole> b)
    {
        b.ToTable("BusinessPartnerRole", Schemas.Mdm);
        b.Property(x => x.ReconciliationAccountType)
            .HasConversion<AccountTypeConverter>().HasMaxLength(1);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

public class PartnerRoleCategoryCompatibilityConfiguration
    : IEntityTypeConfiguration<PartnerRoleCategoryCompatibility>
{
    public void Configure(EntityTypeBuilder<PartnerRoleCategoryCompatibility> b)
    {
        b.ToTable("BusinessPartnerRoleCategory", Schemas.Mdm);
        b.HasIndex(x => new { x.TenantId, x.PartnerRoleId, x.Category }).IsUnique();
        b.HasOne(x => x.PartnerRole).WithMany().HasForeignKey(x => x.PartnerRoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PartnerRoleAssignmentConfiguration : IEntityTypeConfiguration<PartnerRoleAssignment>
{
    public void Configure(EntityTypeBuilder<PartnerRoleAssignment> b)
    {
        b.ToTable("BusinessPartnerRoleAssignment", Schemas.Mdm);

        b.HasIndex(x => new { x.TenantId, x.PartnerId, x.PartnerRoleId })
            .IsUnique()
            .HasFilter($"[ValidTo] = '{DateRange.OpenEnded:yyyy-MM-dd}'")
            .HasDatabaseName("UX_BpRoleAssignment_Open");

        b.HasOne(x => x.Partner).WithMany(x => x.RoleAssignments)
            .HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.PartnerRole).WithMany().HasForeignKey(x => x.PartnerRoleId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(t => t.HasCheckConstraint(
            "CK_BpRoleAssignment_Validity", "[ValidTo] > [ValidFrom]"));
    }
}

public class PartnerAddressConfiguration : IEntityTypeConfiguration<PartnerAddress>
{
    public void Configure(EntityTypeBuilder<PartnerAddress> b)
    {
        b.ToTable("BusinessPartnerAddress", Schemas.Mdm);
        b.HasIndex(x => new { x.TenantId, x.PartnerId, x.AddressType });

        // At most one default address per type.
        b.HasIndex(x => new { x.TenantId, x.PartnerId, x.AddressType, x.IsDefault })
            .IsUnique()
            .HasFilter("[IsDefault] = 1")
            .HasDatabaseName("UX_BpAddress_OneDefaultPerType");

        b.HasOne(x => x.Partner).WithMany(x => x.Addresses)
            .HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PartnerCommunicationConfiguration : IEntityTypeConfiguration<PartnerCommunication>
{
    public void Configure(EntityTypeBuilder<PartnerCommunication> b)
    {
        b.ToTable("BusinessPartnerCommunication", Schemas.Mdm);
        b.HasIndex(x => new { x.TenantId, x.PartnerAddressId, x.CommunicationType });
        b.HasOne(x => x.PartnerAddress).WithMany(x => x.Communications)
            .HasForeignKey(x => x.PartnerAddressId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PartnerIdentificationConfiguration : IEntityTypeConfiguration<PartnerIdentification>
{
    public void Configure(EntityTypeBuilder<PartnerIdentification> b)
    {
        b.ToTable("BusinessPartnerIdentification", Schemas.Mdm);
        b.HasIndex(x => new { x.TenantId, x.PartnerId, x.IdentificationType });
        b.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PartnerTaxNumberConfiguration : IEntityTypeConfiguration<PartnerTaxNumber>
{
    public void Configure(EntityTypeBuilder<PartnerTaxNumber> b)
    {
        b.ToTable("BusinessPartnerTaxNumber", Schemas.Mdm);

        // Duplicate prevention: one tax number per country and type, tenant-wide.
        b.HasIndex(x => new { x.TenantId, x.CountryCode, x.TaxNumberType, x.TaxNumber })
            .IsUnique().HasDatabaseName("UX_BpTaxNumber_Unique");

        b.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PartnerBankConfiguration : IEntityTypeConfiguration<PartnerBank>
{
    public void Configure(EntityTypeBuilder<PartnerBank> b)
    {
        b.ToTable("BusinessPartnerBank", Schemas.Mdm);
        b.HasIndex(x => new { x.TenantId, x.PartnerId });
        b.HasIndex(x => new { x.TenantId, x.CountryCode, x.BankKey, x.AccountNumber })
            .IsUnique().HasDatabaseName("UX_BpBank_Account");

        b.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PartnerRelationshipConfiguration : IEntityTypeConfiguration<PartnerRelationship>
{
    public void Configure(EntityTypeBuilder<PartnerRelationship> b)
    {
        b.ToTable("BusinessPartnerRelationship", Schemas.Mdm);

        b.HasIndex(x => new
        {
            x.TenantId, x.SourcePartnerId, x.TargetPartnerId, x.RelationshipType,
        }).IsUnique()
          .HasFilter($"[ValidTo] = '{DateRange.OpenEnded:yyyy-MM-dd}'")
          .HasDatabaseName("UX_BpRelationship_Open");

        b.HasOne(x => x.SourcePartner).WithMany().HasForeignKey(x => x.SourcePartnerId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.TargetPartner).WithMany().HasForeignKey(x => x.TargetPartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(t => t.HasCheckConstraint(
            "CK_BpRelationship_NotSelf", "[SourcePartnerId] <> [TargetPartnerId]"));
    }
}

public class PartnerCompanyCodeConfiguration : IEntityTypeConfiguration<PartnerCompanyCode>
{
    public void Configure(EntityTypeBuilder<PartnerCompanyCode> b)
    {
        b.ToTable("BusinessPartnerCompanyCode", Schemas.Mdm);
        b.HasIndex(x => new { x.TenantId, x.PartnerId, x.CompanyCodeId }).IsUnique();

        b.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.CompanyCode).WithMany().HasForeignKey(x => x.CompanyCodeId)
            .OnDelete(DeleteBehavior.Restrict);

        // The reconciliation account FK is declared by the Finance module, which
        // owns fin.GLAccount.
    }
}

public class PartnerCustomerConfiguration : IEntityTypeConfiguration<PartnerCustomer>
{
    public void Configure(EntityTypeBuilder<PartnerCustomer> b)
    {
        b.ToTable("BusinessPartnerCustomer", Schemas.Mdm);
        b.HasIndex(x => x.PartnerCompanyCodeId).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.CustomerAccountNumber });

        b.HasOne(x => x.PartnerCompanyCode).WithOne()
            .HasForeignKey<PartnerCustomer>(x => x.PartnerCompanyCodeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PartnerVendorConfiguration : IEntityTypeConfiguration<PartnerVendor>
{
    public void Configure(EntityTypeBuilder<PartnerVendor> b)
    {
        b.ToTable("BusinessPartnerVendor", Schemas.Mdm);
        b.HasIndex(x => x.PartnerCompanyCodeId).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.VendorAccountNumber });

        b.HasOne(x => x.PartnerCompanyCode).WithOne()
            .HasForeignKey<PartnerVendor>(x => x.PartnerCompanyCodeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PartnerSalesAreaConfiguration : IEntityTypeConfiguration<PartnerSalesArea>
{
    public void Configure(EntityTypeBuilder<PartnerSalesArea> b)
    {
        b.ToTable("BusinessPartnerSalesArea", Schemas.Mdm);
        b.HasIndex(x => new
        {
            x.TenantId, x.PartnerId, x.SalesOrganizationId, x.DistributionChannel, x.Division,
        }).IsUnique();

        b.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.SalesOrganization).WithMany().HasForeignKey(x => x.SalesOrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Currency>().WithMany().HasForeignKey(x => x.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PartnerPurchasingOrganizationConfiguration
    : IEntityTypeConfiguration<PartnerPurchasingOrganization>
{
    public void Configure(EntityTypeBuilder<PartnerPurchasingOrganization> b)
    {
        b.ToTable("BusinessPartnerPurchasingOrganization", Schemas.Mdm);
        b.HasIndex(x => new { x.TenantId, x.PartnerId, x.PurchasingOrganizationId }).IsUnique();

        b.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.PurchasingOrganization).WithMany()
            .HasForeignKey(x => x.PurchasingOrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Currency>().WithMany().HasForeignKey(x => x.OrderCurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PartnerCreditProfileConfiguration : IEntityTypeConfiguration<PartnerCreditProfile>
{
    public void Configure(EntityTypeBuilder<PartnerCreditProfile> b)
    {
        b.ToTable("BusinessPartnerCreditProfile", Schemas.Mdm);
        b.HasIndex(x => new { x.TenantId, x.PartnerId, x.CreditControlAreaId }).IsUnique();

        b.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.CreditControlArea).WithMany().HasForeignKey(x => x.CreditControlAreaId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Currency>().WithMany().HasForeignKey(x => x.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(t => t.HasCheckConstraint(
            "CK_BpCreditProfile_Limit", "[CreditLimit] >= 0"));
    }
}
