using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using S4HERP.BuildingBlocks.Infrastructure;
using S4HERP.Controlling.Domain;
using S4HERP.Organization.Domain;

namespace S4HERP.Controlling.Infrastructure;

public class ControllingAreaConfiguration : IEntityTypeConfiguration<ControllingArea>
{
    public void Configure(EntityTypeBuilder<ControllingArea> b)
    {
        b.ToTable("ControllingArea", Schemas.Co);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.HasOne<Currency>().WithMany().HasForeignKey(x => x.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FiscalYearVariant>().WithMany().HasForeignKey(x => x.FiscalYearVariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ControllingAreaCompanyCodeConfiguration
    : IEntityTypeConfiguration<ControllingAreaCompanyCode>
{
    public void Configure(EntityTypeBuilder<ControllingAreaCompanyCode> b)
    {
        b.ToTable("ControllingAreaCompanyCode", Schemas.Co);

        // Rule R8: at most one open-ended assignment per pair. Overlap of closed
        // ranges is checked in the application, which can report which range clashes.
        b.HasIndex(x => new { x.TenantId, x.ControllingAreaId, x.CompanyCodeId })
            .IsUnique()
            .HasFilter($"[ValidTo] = '{S4HERP.BuildingBlocks.Domain.DateRange.OpenEnded:yyyy-MM-dd}'")
            .HasDatabaseName("UX_ControllingAreaCompanyCode_Open");

        b.HasOne(x => x.ControllingArea).WithMany().HasForeignKey(x => x.ControllingAreaId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.CompanyCode).WithMany().HasForeignKey(x => x.CompanyCodeId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(t => t.HasCheckConstraint(
            "CK_ControllingAreaCompanyCode_Validity", "[ValidTo] > [ValidFrom]"));
    }
}

public class CostCenterConfiguration : IEntityTypeConfiguration<CostCenter>
{
    public void Configure(EntityTypeBuilder<CostCenter> b)
    {
        b.ToTable("CostCenter", Schemas.Co);
        b.HasIndex(x => new { x.TenantId, x.ControllingAreaId, x.Code, x.ValidFrom }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.CompanyCodeId });

        b.HasOne(x => x.ControllingArea).WithMany().HasForeignKey(x => x.ControllingAreaId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CompanyCode).WithMany().HasForeignKey(x => x.CompanyCodeId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ProfitCenter).WithMany().HasForeignKey(x => x.ProfitCenterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ParentCostCenter).WithMany().HasForeignKey(x => x.ParentCostCenterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FunctionalArea>().WithMany().HasForeignKey(x => x.FunctionalAreaId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Currency>().WithMany().HasForeignKey(x => x.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(t => t.HasCheckConstraint("CK_CostCenter_Validity", "[ValidTo] > [ValidFrom]"));
    }
}

public class ProfitCenterConfiguration : IEntityTypeConfiguration<ProfitCenter>
{
    public void Configure(EntityTypeBuilder<ProfitCenter> b)
    {
        b.ToTable("ProfitCenter", Schemas.Co);
        b.HasIndex(x => new { x.TenantId, x.ControllingAreaId, x.Code, x.ValidFrom }).IsUnique();

        b.HasOne(x => x.ControllingArea).WithMany().HasForeignKey(x => x.ControllingAreaId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ParentProfitCenter).WithMany().HasForeignKey(x => x.ParentProfitCenterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Segment>().WithMany().HasForeignKey(x => x.SegmentId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(t => t.HasCheckConstraint("CK_ProfitCenter_Validity", "[ValidTo] > [ValidFrom]"));
    }
}

public class InternalOrderTypeConfiguration : IEntityTypeConfiguration<InternalOrderType>
{
    public void Configure(EntityTypeBuilder<InternalOrderType> b)
    {
        b.ToTable("InternalOrderType", Schemas.Co);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

public class InternalOrderConfiguration : IEntityTypeConfiguration<InternalOrder>
{
    public void Configure(EntityTypeBuilder<InternalOrder> b)
    {
        b.ToTable("InternalOrder", Schemas.Co);
        b.Property(x => x.WarningTolerancePercent).HasColumnType(ColumnTypes.Rate);
        b.Property(x => x.BlockingTolerancePercent).HasColumnType(ColumnTypes.Rate);

        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.ControllingAreaId, x.IsClosed });

        b.HasOne(x => x.InternalOrderType).WithMany().HasForeignKey(x => x.InternalOrderTypeId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ControllingArea).WithMany().HasForeignKey(x => x.ControllingAreaId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CompanyCode).WithMany().HasForeignKey(x => x.CompanyCodeId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ResponsibleCostCenter).WithMany()
            .HasForeignKey(x => x.ResponsibleCostCenterId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProfitCenter>().WithMany().HasForeignKey(x => x.ProfitCenterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Currency>().WithMany().HasForeignKey(x => x.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(t => t.HasCheckConstraint(
            "CK_InternalOrder_Tolerances",
            "([WarningTolerancePercent] IS NULL OR [WarningTolerancePercent] BETWEEN 0 AND 200) AND " +
            "([BlockingTolerancePercent] IS NULL OR [BlockingTolerancePercent] BETWEEN 0 AND 200)"));
    }
}

public class InternalOrderSettlementRuleConfiguration
    : IEntityTypeConfiguration<InternalOrderSettlementRule>
{
    public void Configure(EntityTypeBuilder<InternalOrderSettlementRule> b)
    {
        b.ToTable("InternalOrderSettlementRule", Schemas.Co);
        b.Property(x => x.SharePercent).HasColumnType(ColumnTypes.Rate);
        b.HasIndex(x => new { x.TenantId, x.InternalOrderId });

        b.HasOne(x => x.InternalOrder).WithMany().HasForeignKey(x => x.InternalOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.ToTable(t => t.HasCheckConstraint(
            "CK_SettlementRule_Share", "[SharePercent] > 0 AND [SharePercent] <= 100"));
    }
}

public class AllocationCycleConfiguration : IEntityTypeConfiguration<AllocationCycle>
{
    public void Configure(EntityTypeBuilder<AllocationCycle> b)
    {
        b.ToTable("AllocationCycle", Schemas.Co);
        b.HasIndex(x => new { x.TenantId, x.ControllingAreaId, x.Code, x.ValidFrom }).IsUnique();
        b.HasOne(x => x.ControllingArea).WithMany().HasForeignKey(x => x.ControllingAreaId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(t => t.HasCheckConstraint(
            "CK_AllocationCycle_Assessment",
            "([Method] = 2 AND [AssessmentCostElementId] IS NOT NULL) OR [Method] = 1"));
    }
}

public class AllocationCycleSegmentConfiguration : IEntityTypeConfiguration<AllocationCycleSegment>
{
    public void Configure(EntityTypeBuilder<AllocationCycleSegment> b)
    {
        b.ToTable("AllocationCycleSegment", Schemas.Co);
        b.Property(x => x.FixedPercent).HasColumnType(ColumnTypes.Rate);
        b.Property(x => x.FixedPortion).HasColumnType(ColumnTypes.Rate);

        b.HasIndex(x => new { x.TenantId, x.AllocationCycleId, x.SegmentNumber }).IsUnique();

        b.HasOne(x => x.AllocationCycle).WithMany().HasForeignKey(x => x.AllocationCycleId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<CostCenter>().WithMany().HasForeignKey(x => x.SenderCostCenterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CostCenter>().WithMany().HasForeignKey(x => x.ReceiverCostCenterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<InternalOrder>().WithMany().HasForeignKey(x => x.ReceiverInternalOrderId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StatisticalKeyFigure>().WithMany().HasForeignKey(x => x.StatisticalKeyFigureId)
            .OnDelete(DeleteBehavior.Restrict);

        // Exactly one receiver, and exactly one tracing factor.
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_AllocationSegment_OneReceiver",
                "(CASE WHEN [ReceiverCostCenterId] IS NULL THEN 0 ELSE 1 END + " +
                " CASE WHEN [ReceiverInternalOrderId] IS NULL THEN 0 ELSE 1 END) = 1");
            t.HasCheckConstraint("CK_AllocationSegment_OneTracingFactor",
                "(CASE WHEN [FixedPercent] IS NULL THEN 0 ELSE 1 END + " +
                " CASE WHEN [FixedPortion] IS NULL THEN 0 ELSE 1 END + " +
                " CASE WHEN [StatisticalKeyFigureId] IS NULL THEN 0 ELSE 1 END) = 1");
        });
    }
}

public class StatisticalKeyFigureConfiguration : IEntityTypeConfiguration<StatisticalKeyFigure>
{
    public void Configure(EntityTypeBuilder<StatisticalKeyFigure> b)
    {
        b.ToTable("StatisticalKeyFigure", Schemas.Co);
        b.HasIndex(x => new { x.TenantId, x.ControllingAreaId, x.Code }).IsUnique();
        b.HasOne(x => x.ControllingArea).WithMany().HasForeignKey(x => x.ControllingAreaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
