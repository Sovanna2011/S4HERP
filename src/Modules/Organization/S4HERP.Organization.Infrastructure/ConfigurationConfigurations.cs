using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using S4HERP.BuildingBlocks.Infrastructure;
using S4HERP.Organization.Domain;

namespace S4HERP.Organization.Infrastructure;

/// <summary>
/// Stores <see cref="AccountType"/> as its single-character code (S/D/K/A/M)
/// rather than an ordinal, so the column is readable in SE16N and in raw SQL.
/// </summary>
public sealed class AccountTypeConverter()
    : ValueConverter<AccountType, string>(
        v => ((char)v).ToString(),
        v => (AccountType)v[0]);

public class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> b)
    {
        b.ToTable("Currency", Schemas.Cfg);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

public class ExchangeRateTypeConfiguration : IEntityTypeConfiguration<ExchangeRateType>
{
    public void Configure(EntityTypeBuilder<ExchangeRateType> b)
    {
        b.ToTable("ExchangeRateType", Schemas.Cfg);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

public class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> b)
    {
        b.ToTable("ExchangeRate", Schemas.Cfg);
        b.Property(x => x.Rate).HasColumnType(ColumnTypes.Rate);

        b.HasIndex(x => new
        {
            x.TenantId, x.ExchangeRateTypeId, x.FromCurrencyId, x.ToCurrencyId, x.ValidFrom,
        }).IsUnique();

        b.HasOne(x => x.ExchangeRateType).WithMany().HasForeignKey(x => x.ExchangeRateTypeId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Currency>().WithMany().HasForeignKey(x => x.FromCurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Currency>().WithMany().HasForeignKey(x => x.ToCurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(t => t.HasCheckConstraint("CK_ExchangeRate_Positive", "[Rate] > 0"));
    }
}

public class FiscalYearVariantConfiguration : IEntityTypeConfiguration<FiscalYearVariant>
{
    public void Configure(EntityTypeBuilder<FiscalYearVariant> b)
    {
        b.ToTable("FiscalYearVariant", Schemas.Cfg);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

public class FiscalYearPeriodConfiguration : IEntityTypeConfiguration<FiscalYearPeriod>
{
    public void Configure(EntityTypeBuilder<FiscalYearPeriod> b)
    {
        b.ToTable("FiscalYearPeriod", Schemas.Cfg);
        b.HasIndex(x => new { x.TenantId, x.FiscalYearVariantId, x.FiscalYear, x.Period })
            .IsUnique();

        // Period lookup by posting date is the hottest read in the posting engine.
        b.HasIndex(x => new { x.TenantId, x.FiscalYearVariantId, x.StartDate, x.EndDate });

        b.HasOne(x => x.FiscalYearVariant).WithMany().HasForeignKey(x => x.FiscalYearVariantId)
            .OnDelete(DeleteBehavior.Cascade);

        b.ToTable(t => t.HasCheckConstraint(
            "CK_FiscalYearPeriod_Dates", "[EndDate] >= [StartDate]"));
    }
}

public class PostingPeriodVariantConfiguration : IEntityTypeConfiguration<PostingPeriodVariant>
{
    public void Configure(EntityTypeBuilder<PostingPeriodVariant> b)
    {
        b.ToTable("PostingPeriodVariant", Schemas.Cfg);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

public class PostingPeriodConfiguration : IEntityTypeConfiguration<PostingPeriod>
{
    public void Configure(EntityTypeBuilder<PostingPeriod> b)
    {
        b.ToTable("PostingPeriod", Schemas.Cfg);
        b.Property(x => x.AccountType).HasConversion<AccountTypeConverter>().HasMaxLength(1);

        b.HasIndex(x => new
        {
            x.TenantId, x.PostingPeriodVariantId, x.AccountType, x.FiscalYear, x.Period,
        }).IsUnique();

        b.HasOne(x => x.PostingPeriodVariant).WithMany().HasForeignKey(x => x.PostingPeriodVariantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DocumentTypeConfiguration : IEntityTypeConfiguration<DocumentType>
{
    public void Configure(EntityTypeBuilder<DocumentType> b)
    {
        b.ToTable("DocumentType", Schemas.Cfg);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

public class NumberRangeConfiguration : IEntityTypeConfiguration<NumberRange>
{
    public void Configure(EntityTypeBuilder<NumberRange> b)
    {
        b.ToTable("NumberRange", Schemas.Cfg);

        b.HasIndex(x => new { x.TenantId, x.ObjectType, x.Code, x.CompanyCodeId, x.FiscalYear })
            .IsUnique();

        b.HasOne(x => x.CompanyCode).WithMany().HasForeignKey(x => x.CompanyCodeId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(t => t.HasCheckConstraint(
            "CK_NumberRange_Bounds",
            "[ToNumber] >= [FromNumber] AND [CurrentNumber] >= [FromNumber] - 1 AND [CurrentNumber] <= [ToNumber]"));
    }
}

public class NumberRangeGapConfiguration : IEntityTypeConfiguration<NumberRangeGap>
{
    public void Configure(EntityTypeBuilder<NumberRangeGap> b)
    {
        b.ToTable("NumberRangeGap", Schemas.Cfg);
        b.HasIndex(x => new { x.TenantId, x.NumberRangeId, x.GapNumber }).IsUnique();
        b.HasOne(x => x.NumberRange).WithMany().HasForeignKey(x => x.NumberRangeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PostingKeyConfiguration : IEntityTypeConfiguration<PostingKey>
{
    public void Configure(EntityTypeBuilder<PostingKey> b)
    {
        b.ToTable("PostingKey", Schemas.Cfg);
        b.Property(x => x.AccountType).HasConversion<AccountTypeConverter>().HasMaxLength(1);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

public class TaxCodeConfiguration : IEntityTypeConfiguration<TaxCode>
{
    public void Configure(EntityTypeBuilder<TaxCode> b)
    {
        b.ToTable("TaxCode", Schemas.Cfg);
        b.Property(x => x.Rate).HasColumnType(ColumnTypes.Rate);
        b.HasIndex(x => new { x.TenantId, x.CountryCode, x.Code, x.ValidFrom }).IsUnique();

        b.ToTable(t => t.HasCheckConstraint(
            "CK_TaxCode_Rate", "[Rate] >= 0 AND [Rate] <= 100"));
    }
}

public class PaymentTermConfiguration : IEntityTypeConfiguration<PaymentTerm>
{
    public void Configure(EntityTypeBuilder<PaymentTerm> b)
    {
        b.ToTable("PaymentTerm", Schemas.Cfg);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();

        b.Property(x => x.CashDiscount1Percent).HasColumnType(ColumnTypes.Rate);
        b.Property(x => x.CashDiscount2Percent).HasColumnType(ColumnTypes.Rate);

        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_PaymentTerm_NetDays", "[NetDays] >= 0");

            // A discount tier is days *and* a percentage or neither. Half a tier
            // silently never applies, which is the worst way for it to be wrong.
            //
            // Spelled out rather than as `(a IS NULL) = (b IS NULL)`: T-SQL has no
            // boolean type, so comparing two predicates with = is a syntax error —
            // one SQL Server only reports when the migration runs.
            t.HasCheckConstraint(
                "CK_PaymentTerm_Discount1",
                "([CashDiscount1Days] IS NULL AND [CashDiscount1Percent] IS NULL) "
                + "OR ([CashDiscount1Days] IS NOT NULL AND [CashDiscount1Percent] IS NOT NULL)");
            t.HasCheckConstraint(
                "CK_PaymentTerm_Discount2",
                "([CashDiscount2Days] IS NULL AND [CashDiscount2Percent] IS NULL) "
                + "OR ([CashDiscount2Days] IS NOT NULL AND [CashDiscount2Percent] IS NOT NULL)");
            t.HasCheckConstraint(
                "CK_PaymentTerm_DiscountRate",
                "([CashDiscount1Percent] IS NULL OR ([CashDiscount1Percent] >= 0 AND [CashDiscount1Percent] <= 100)) "
                + "AND ([CashDiscount2Percent] IS NULL OR ([CashDiscount2Percent] >= 0 AND [CashDiscount2Percent] <= 100))");

            // Tier 2 is the longer, smaller one. Reversed, tier 1 would shadow it
            // for its whole window and tier 2 would be unreachable.
            t.HasCheckConstraint(
                "CK_PaymentTerm_DiscountOrder",
                "[CashDiscount1Days] IS NULL OR [CashDiscount2Days] IS NULL "
                + "OR [CashDiscount2Days] > [CashDiscount1Days]");
        });
    }
}

public class TransactionCodeConfiguration : IEntityTypeConfiguration<TransactionCode>
{
    public void Configure(EntityTypeBuilder<TransactionCode> b)
    {
        b.ToTable("TransactionCode", Schemas.Cfg);
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.Category });
    }
}
