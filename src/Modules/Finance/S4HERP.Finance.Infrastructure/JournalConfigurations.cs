using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using S4HERP.BuildingBlocks.Infrastructure;
using S4HERP.Finance.Domain;
using S4HERP.Organization.Domain;
using S4HERP.Organization.Infrastructure;

namespace S4HERP.Finance.Infrastructure;

public class JournalEntryHeaderConfiguration : IEntityTypeConfiguration<JournalEntryHeader>
{
    public void Configure(EntityTypeBuilder<JournalEntryHeader> b)
    {
        b.ToTable("JournalEntryHeader", Schemas.Fin);

        b.HasKey(x => new { x.TenantId, x.CompanyCodeId, x.FiscalYear, x.DocumentNumber });

        b.Property(x => x.ExchangeRateToLocal).HasColumnType(ColumnTypes.Rate);
        b.Property(x => x.ExchangeRateToGroup).HasColumnType(ColumnTypes.Rate);

        // Unique per company code, not tenant-wide: two legal entities draw from
        // their own number ranges and legitimately share a document number.
        b.HasIndex(x => new { x.TenantId, x.CompanyCodeId, x.DocumentNumberFormatted })
            .IsUnique();
        b.HasIndex(x => new { x.TenantId, x.CompanyCodeId, x.PostingDate });
        b.HasIndex(x => new { x.TenantId, x.Status });
        b.HasIndex(x => x.IntercompanyTransactionId)
            .HasFilter("[IntercompanyTransactionId] IS NOT NULL");

        b.HasOne(x => x.CompanyCode).WithMany().HasForeignKey(x => x.CompanyCodeId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.DocumentType).WithMany().HasForeignKey(x => x.DocumentTypeId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Currency>().WithMany().HasForeignKey(x => x.DocumentCurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ExchangeRateType>().WithMany().HasForeignKey(x => x.ExchangeRateTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(t =>
        {
            // db/scripts/050 puts triggers on this table. SQL Server rejects an
            // OUTPUT clause without INTO on a table with triggers, and EF emits
            // one by default, so the model has to declare them.
            t.HasTrigger("TR_JournalEntryHeader_PostedImmutable");
            t.HasTrigger("TR_JournalEntryHeader_NoDeletePosted");

            t.HasCheckConstraint("CK_JournalHeader_Period", "[FiscalPeriod] BETWEEN 1 AND 16");
            t.HasCheckConstraint("CK_JournalHeader_Rates",
                "[ExchangeRateToLocal] > 0 AND [ExchangeRateToGroup] > 0");
            // A document cannot both reverse and be reversed by the same document.
            t.HasCheckConstraint("CK_JournalHeader_Reversal",
                "[ReversalOfDocumentNumber] IS NULL OR [ReversedByDocumentNumber] IS NULL OR " +
                "[ReversalOfDocumentNumber] <> [ReversedByDocumentNumber]");
        });
    }
}

public class JournalEntryLineConfiguration : IEntityTypeConfiguration<JournalEntryLine>
{
    public void Configure(EntityTypeBuilder<JournalEntryLine> b)
    {
        b.ToTable("JournalEntryLine", Schemas.Fin);

        b.HasKey(x => new
        {
            x.TenantId, x.CompanyCodeId, x.FiscalYear, x.DocumentNumber, x.LedgerId, x.LineNumber,
        });

        b.Property(x => x.AccountType).HasConversion<AccountTypeConverter>().HasMaxLength(1);
        b.Property(x => x.DebitCredit).HasConversion<DebitCreditConverter>().HasMaxLength(1);
        b.Property(x => x.Quantity).HasColumnType(ColumnTypes.Rate);

        b.HasOne(x => x.Header).WithMany(x => x.Lines)
            .HasForeignKey(x => new { x.TenantId, x.CompanyCodeId, x.FiscalYear, x.DocumentNumber })
            .OnDelete(DeleteBehavior.Cascade);

        // Reporting access paths. Dimension columns deliberately carry no foreign
        // keys (ADR-18): validity is enforced by the posting engine, and per-row
        // constraint checks on the largest table in the system are not worth their
        // insert cost.
        b.HasIndex(x => new { x.TenantId, x.CompanyCodeId, x.LedgerId, x.GLAccountId })
            .IncludeProperties(x => new { x.LocalAmount, x.GroupAmount, x.DocumentAmount })
            .HasDatabaseName("IX_JournalEntryLine_Account");

        b.HasIndex(x => new { x.TenantId, x.CompanyCodeId, x.BusinessPartnerId })
            .HasFilter("[BusinessPartnerId] IS NOT NULL")
            .HasDatabaseName("IX_JournalEntryLine_Partner");

        b.HasIndex(x => new { x.TenantId, x.CostCenterId })
            .HasFilter("[CostCenterId] IS NOT NULL")
            .HasDatabaseName("IX_JournalEntryLine_CostCenter");

        b.HasIndex(x => new { x.TenantId, x.ProfitCenterId })
            .HasFilter("[ProfitCenterId] IS NOT NULL")
            .HasDatabaseName("IX_JournalEntryLine_ProfitCenter");

        b.ToTable(t =>
        {
            t.HasTrigger("TR_JournalEntryLine_NoUpdate");
            t.HasTrigger("TR_JournalEntryLine_NoDelete");

            // Signed amounts: debit positive, credit negative, so a balanced
            // document sums to zero in every currency.
            t.HasCheckConstraint("CK_JournalEntryLine_Sign",
                "([DebitCredit] = 'D' AND [DocumentAmount] >= 0) OR " +
                "([DebitCredit] = 'C' AND [DocumentAmount] <= 0)");
            t.HasCheckConstraint("CK_JournalEntryLine_LocalSign",
                "([DebitCredit] = 'D' AND [LocalAmount] >= 0) OR " +
                "([DebitCredit] = 'C' AND [LocalAmount] <= 0)");
            // Exactly one subledger object per account type.
            t.HasCheckConstraint("CK_JournalEntryLine_AccountObject",
                "([AccountType] = 'S' AND [GLAccountId] IS NOT NULL) OR " +
                "([AccountType] IN ('D','K') AND [BusinessPartnerId] IS NOT NULL) OR " +
                "([AccountType] = 'A' AND [AssetId] IS NOT NULL) OR " +
                "([AccountType] = 'M')");
            t.HasCheckConstraint("CK_JournalEntryLine_LineNumber", "[LineNumber] > 0");
        });
    }
}

public class OpenItemConfiguration : IEntityTypeConfiguration<OpenItem>
{
    public void Configure(EntityTypeBuilder<OpenItem> b)
    {
        b.ToTable("OpenItem", Schemas.Fin);
        b.Property(x => x.AccountType).HasConversion<AccountTypeConverter>().HasMaxLength(1);

        b.HasIndex(x => new
        {
            x.TenantId, x.CompanyCodeId, x.FiscalYear, x.DocumentNumber, x.LedgerId, x.LineNumber,
        }).IsUnique().HasDatabaseName("UX_OpenItem_JournalLine");

        // The aging, dunning and payment-run access path.
        b.HasIndex(x => new
        {
            x.TenantId, x.CompanyCodeId, x.BusinessPartnerId, x.ClearingStatus, x.DueDate,
        }).HasDatabaseName("IX_OpenItem_PartnerDue");

        b.HasIndex(x => new { x.TenantId, x.CompanyCodeId, x.GLAccountId, x.ClearingStatus })
            .HasFilter("[GLAccountId] IS NOT NULL")
            .HasDatabaseName("IX_OpenItem_Account");

        b.HasOne(x => x.JournalEntryLine).WithMany()
            .HasForeignKey(x => new
            {
                x.TenantId, x.CompanyCodeId, x.FiscalYear, x.DocumentNumber, x.LedgerId, x.LineNumber,
            })
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_OpenItem_OpenWithinOriginal",
                "ABS([OpenAmountDocument]) <= ABS([OriginalAmountDocument])");
            t.HasCheckConstraint("CK_OpenItem_ClearedConsistent",
                "([ClearingStatus] = 3 AND [OpenAmountDocument] = 0 AND [ClearingDocumentNumber] IS NOT NULL) OR " +
                "([ClearingStatus] <> 3)");
        });
    }
}

public class ClearingHeaderConfiguration : IEntityTypeConfiguration<ClearingHeader>
{
    public void Configure(EntityTypeBuilder<ClearingHeader> b)
    {
        b.ToTable("ClearingHeader", Schemas.Fin);
        b.HasIndex(x => new
        {
            x.TenantId, x.CompanyCodeId, x.FiscalYear, x.ClearingDocumentNumber,
        }).IsUnique();

        b.HasOne<CompanyCode>().WithMany().HasForeignKey(x => x.CompanyCodeId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Currency>().WithMany().HasForeignKey(x => x.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ClearingLineConfiguration : IEntityTypeConfiguration<ClearingLine>
{
    public void Configure(EntityTypeBuilder<ClearingLine> b)
    {
        b.ToTable("ClearingLine", Schemas.Fin);
        b.HasIndex(x => new { x.TenantId, x.OpenItemId });

        b.HasOne(x => x.ClearingHeader).WithMany(x => x.Lines)
            .HasForeignKey(x => x.ClearingHeaderId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.OpenItem).WithMany()
            .HasForeignKey(x => x.OpenItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PostingIdempotencyConfiguration : IEntityTypeConfiguration<PostingIdempotency>
{
    public void Configure(EntityTypeBuilder<PostingIdempotency> b)
    {
        b.ToTable("PostingIdempotency", Schemas.Fin);
        b.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
    }
}

public class HouseBankConfiguration : IEntityTypeConfiguration<HouseBank>
{
    public void Configure(EntityTypeBuilder<HouseBank> b)
    {
        b.ToTable("HouseBank", Schemas.Fin);
        b.HasIndex(x => new { x.TenantId, x.CompanyCodeId, x.Code }).IsUnique();

        b.HasOne(x => x.CompanyCode).WithMany().HasForeignKey(x => x.CompanyCodeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class HouseBankAccountConfiguration : IEntityTypeConfiguration<HouseBankAccount>
{
    public void Configure(EntityTypeBuilder<HouseBankAccount> b)
    {
        b.ToTable("HouseBankAccount", Schemas.Fin);
        b.HasIndex(x => new { x.TenantId, x.HouseBankId, x.Code }).IsUnique();

        b.HasOne(x => x.HouseBank).WithMany(x => x.Accounts)
            .HasForeignKey(x => x.HouseBankId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.GLAccount).WithMany().HasForeignKey(x => x.GLAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Currency>().WithMany().HasForeignKey(x => x.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentRunConfiguration : IEntityTypeConfiguration<PaymentRun>
{
    public void Configure(EntityTypeBuilder<PaymentRun> b)
    {
        b.ToTable("PaymentRun", Schemas.Fin);
        b.HasIndex(x => new { x.TenantId, x.RunId }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.CompanyCodeId, x.Status });

        b.HasOne(x => x.CompanyCode).WithMany().HasForeignKey(x => x.CompanyCodeId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.HouseBankAccount).WithMany().HasForeignKey(x => x.HouseBankAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentRunItemConfiguration : IEntityTypeConfiguration<PaymentRunItem>
{
    public void Configure(EntityTypeBuilder<PaymentRunItem> b)
    {
        b.ToTable("PaymentRunItem", Schemas.Fin);
        b.HasIndex(x => new { x.TenantId, x.PaymentRunId, x.IsExcluded });

        b.HasOne(x => x.PaymentRun).WithMany(x => x.Items)
            .HasForeignKey(x => x.PaymentRunId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.OpenItem).WithMany().HasForeignKey(x => x.OpenItemId)
            .OnDelete(DeleteBehavior.Restrict);

        // An excluded item states why. Silence would be indistinguishable from
        // an item that was simply never considered.
        b.ToTable(t => t.HasCheckConstraint(
            "CK_PaymentRunItem_Exclusion",
            "[IsExcluded] = 0 OR [ExclusionReason] IS NOT NULL"));
    }
}
