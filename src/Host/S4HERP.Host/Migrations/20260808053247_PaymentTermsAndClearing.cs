using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace S4HERP.Host.Migrations
{
    /// <inheritdoc />
    public partial class PaymentTermsAndClearing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentTerm",
                schema: "cfg",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    BaselineDateRule = table.Column<int>(type: "int", nullable: false),
                    NetDays = table.Column<int>(type: "int", nullable: false),
                    CashDiscount1Days = table.Column<int>(type: "int", nullable: true),
                    CashDiscount1Percent = table.Column<decimal>(type: "decimal(23,6)", precision: 19, scale: 4, nullable: true),
                    CashDiscount2Days = table.Column<int>(type: "int", nullable: true),
                    CashDiscount2Percent = table.Column<decimal>(type: "decimal(23,6)", precision: 19, scale: 4, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTerm", x => x.Id);
                    table.CheckConstraint("CK_PaymentTerm_Discount1", "([CashDiscount1Days] IS NULL AND [CashDiscount1Percent] IS NULL) OR ([CashDiscount1Days] IS NOT NULL AND [CashDiscount1Percent] IS NOT NULL)");
                    table.CheckConstraint("CK_PaymentTerm_Discount2", "([CashDiscount2Days] IS NULL AND [CashDiscount2Percent] IS NULL) OR ([CashDiscount2Days] IS NOT NULL AND [CashDiscount2Percent] IS NOT NULL)");
                    table.CheckConstraint("CK_PaymentTerm_DiscountOrder", "[CashDiscount1Days] IS NULL OR [CashDiscount2Days] IS NULL OR [CashDiscount2Days] > [CashDiscount1Days]");
                    table.CheckConstraint("CK_PaymentTerm_DiscountRate", "([CashDiscount1Percent] IS NULL OR ([CashDiscount1Percent] >= 0 AND [CashDiscount1Percent] <= 100)) AND ([CashDiscount2Percent] IS NULL OR ([CashDiscount2Percent] >= 0 AND [CashDiscount2Percent] <= 100))");
                    table.CheckConstraint("CK_PaymentTerm_NetDays", "[NetDays] >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerm_TenantId_Code",
                schema: "cfg",
                table: "PaymentTerm",
                columns: new[] { "TenantId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Same reason as the ApprovalWorkflow migration: sec.TenantIsolationPolicy
            // is SCHEMABINDING, so it blocks dropping any tenant-scoped table until it
            // is gone. db/scripts/020-row-level-security.sql rebuilds it on the next
            // start. Every migration that drops such a table needs these two lines.
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM sys.security_policies WHERE name = N'TenantIsolationPolicy') " +
                "DROP SECURITY POLICY sec.TenantIsolationPolicy;");

            migrationBuilder.DropTable(
                name: "PaymentTerm",
                schema: "cfg");
        }
    }
}
