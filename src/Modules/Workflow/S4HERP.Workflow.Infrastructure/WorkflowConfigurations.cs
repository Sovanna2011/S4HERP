using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using S4HERP.BuildingBlocks.Infrastructure;
using S4HERP.Organization.Domain;
using S4HERP.Workflow.Domain;

namespace S4HERP.Workflow.Infrastructure;

public class ApprovalRuleConfiguration : IEntityTypeConfiguration<ApprovalRule>
{
    public void Configure(EntityTypeBuilder<ApprovalRule> b)
    {
        b.ToTable("ApprovalRule", Schemas.Wf);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.CompanyCodeId, x.DocumentTypeCode, x.FromAmount });

        b.HasOne(x => x.CompanyCode).WithMany().HasForeignKey(x => x.CompanyCodeId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Currency>().WithMany().HasForeignKey(x => x.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(t => t.HasCheckConstraint("CK_ApprovalRule_Amount", "[FromAmount] >= 0"));
    }
}

public class WorkflowInstanceConfiguration : IEntityTypeConfiguration<WorkflowInstance>
{
    public void Configure(EntityTypeBuilder<WorkflowInstance> b)
    {
        b.ToTable("WorkflowInstance", Schemas.Wf);

        // One live workflow per object: a document cannot be in two approval
        // processes at once.
        b.HasIndex(x => new { x.TenantId, x.ObjectType, x.ObjectId })
            .IsUnique()
            .HasFilter("[Status] = 1")
            .HasDatabaseName("UX_WorkflowInstance_OnePending");

        b.HasIndex(x => new { x.TenantId, x.Status, x.CompanyCodeId });

        b.HasOne(x => x.CompanyCode).WithMany().HasForeignKey(x => x.CompanyCodeId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Currency>().WithMany().HasForeignKey(x => x.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WorkflowStepConfiguration : IEntityTypeConfiguration<WorkflowStep>
{
    public void Configure(EntityTypeBuilder<WorkflowStep> b)
    {
        b.ToTable("WorkflowStep", Schemas.Wf);
        b.HasIndex(x => new { x.TenantId, x.WorkflowInstanceId, x.Sequence }).IsUnique();

        b.HasOne(x => x.WorkflowInstance).WithMany(x => x.Steps)
            .HasForeignKey(x => x.WorkflowInstanceId).OnDelete(DeleteBehavior.Cascade);

        // A decided step must record who decided it and when.
        b.ToTable(t => t.HasCheckConstraint(
            "CK_WorkflowStep_Decided",
            "[Decision] = 1 OR ([DecidedBy] IS NOT NULL AND [DecidedAtUtc] IS NOT NULL)"));
    }
}
