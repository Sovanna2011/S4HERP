using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using S4HERP.Audit.Domain;
using S4HERP.BuildingBlocks.Infrastructure;

namespace S4HERP.Audit.Infrastructure;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLog", Schemas.Audit);

        // Time-ordered access is the common one; partitioned by year alongside
        // the journal (see db/scripts/030-partitioning.sql).
        b.HasIndex(x => new { x.TenantId, x.OccurredAtUtc });
        b.HasIndex(x => new { x.TenantId, x.ObjectType, x.ObjectId });
        b.HasIndex(x => new { x.TenantId, x.UserName, x.OccurredAtUtc });
        b.HasIndex(x => x.CorrelationId).HasFilter("[CorrelationId] IS NOT NULL");

        // Append-only trigger from db/scripts/050; see the note in
        // JournalEntryHeaderConfiguration for why EF must know.
        b.ToTable(t => t.HasTrigger("TR_AuditLog_AppendOnly"));
    }
}

public class AuditLogFieldConfiguration : IEntityTypeConfiguration<AuditLogField>
{
    public void Configure(EntityTypeBuilder<AuditLogField> b)
    {
        b.ToTable("AuditLogField", Schemas.Audit);
        b.HasIndex(x => new { x.TenantId, x.AuditLogId });
        b.HasOne(x => x.AuditLog).WithMany(x => x.Fields).HasForeignKey(x => x.AuditLogId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TableBrowserLogConfiguration : IEntityTypeConfiguration<TableBrowserLog>
{
    public void Configure(EntityTypeBuilder<TableBrowserLog> b)
    {
        b.ToTable("TableBrowserLog", Schemas.Audit);
        b.HasIndex(x => new { x.TenantId, x.ExecutedAtUtc });
        b.HasIndex(x => new { x.TenantId, x.UserName, x.ExecutedAtUtc });
        b.HasIndex(x => new { x.TenantId, x.TableName, x.ExecutedAtUtc });
    }
}
