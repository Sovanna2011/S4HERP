using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace S4HERP.Host.Migrations
{
    /// <inheritdoc />
    public partial class PaymentRunAndHouseBanks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HouseBank",
                schema: "fin",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CompanyCodeId = table.Column<long>(type: "bigint", nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    SwiftCode = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: true),
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
                    table.PrimaryKey("PK_HouseBank", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseBank_CompanyCode_CompanyCodeId",
                        column: x => x.CompanyCodeId,
                        principalSchema: "org",
                        principalTable: "CompanyCode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentMethod",
                schema: "cfg",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    RequiresBankDetails = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_PaymentMethod", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HouseBankAccount",
                schema: "fin",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HouseBankId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: false),
                    Iban = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    CurrencyId = table.Column<long>(type: "bigint", nullable: false),
                    GLAccountId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_HouseBankAccount", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseBankAccount_Currency_CurrencyId",
                        column: x => x.CurrencyId,
                        principalSchema: "cfg",
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HouseBankAccount_GLAccount_GLAccountId",
                        column: x => x.GLAccountId,
                        principalSchema: "fin",
                        principalTable: "GLAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HouseBankAccount_HouseBank_HouseBankId",
                        column: x => x.HouseBankId,
                        principalSchema: "fin",
                        principalTable: "HouseBank",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentRun",
                schema: "fin",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CompanyCodeId = table.Column<long>(type: "bigint", nullable: false),
                    RunDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueBy = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentMethodCode = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    HouseBankAccountId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExecutedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ExecutedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentRun", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentRun_CompanyCode_CompanyCodeId",
                        column: x => x.CompanyCodeId,
                        principalSchema: "org",
                        principalTable: "CompanyCode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentRun_HouseBankAccount_HouseBankAccountId",
                        column: x => x.HouseBankAccountId,
                        principalSchema: "fin",
                        principalTable: "HouseBankAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentRunItem",
                schema: "fin",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentRunId = table.Column<long>(type: "bigint", nullable: false),
                    OpenItemId = table.Column<long>(type: "bigint", nullable: false),
                    BusinessPartnerId = table.Column<long>(type: "bigint", nullable: false),
                    BusinessPartnerNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FiscalYear = table.Column<short>(type: "smallint", nullable: false),
                    DocumentNumber = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<short>(type: "smallint", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrencyId = table.Column<long>(type: "bigint", nullable: false),
                    IsExcluded = table.Column<bool>(type: "bit", nullable: false),
                    ExclusionReason = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    PaymentDocumentNumber = table.Column<long>(type: "bigint", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentRunItem", x => x.Id);
                    table.CheckConstraint("CK_PaymentRunItem_Exclusion", "[IsExcluded] = 0 OR [ExclusionReason] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_PaymentRunItem_OpenItem_OpenItemId",
                        column: x => x.OpenItemId,
                        principalSchema: "fin",
                        principalTable: "OpenItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentRunItem_PaymentRun_PaymentRunId",
                        column: x => x.PaymentRunId,
                        principalSchema: "fin",
                        principalTable: "PaymentRun",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HouseBank_CompanyCodeId",
                schema: "fin",
                table: "HouseBank",
                column: "CompanyCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseBank_TenantId_CompanyCodeId_Code",
                schema: "fin",
                table: "HouseBank",
                columns: new[] { "TenantId", "CompanyCodeId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseBankAccount_CurrencyId",
                schema: "fin",
                table: "HouseBankAccount",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseBankAccount_GLAccountId",
                schema: "fin",
                table: "HouseBankAccount",
                column: "GLAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseBankAccount_HouseBankId",
                schema: "fin",
                table: "HouseBankAccount",
                column: "HouseBankId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseBankAccount_TenantId_HouseBankId_Code",
                schema: "fin",
                table: "HouseBankAccount",
                columns: new[] { "TenantId", "HouseBankId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethod_TenantId_Code",
                schema: "cfg",
                table: "PaymentMethod",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRun_CompanyCodeId",
                schema: "fin",
                table: "PaymentRun",
                column: "CompanyCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRun_HouseBankAccountId",
                schema: "fin",
                table: "PaymentRun",
                column: "HouseBankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRun_TenantId_CompanyCodeId_Status",
                schema: "fin",
                table: "PaymentRun",
                columns: new[] { "TenantId", "CompanyCodeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRun_TenantId_RunId",
                schema: "fin",
                table: "PaymentRun",
                columns: new[] { "TenantId", "RunId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRunItem_OpenItemId",
                schema: "fin",
                table: "PaymentRunItem",
                column: "OpenItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRunItem_PaymentRunId",
                schema: "fin",
                table: "PaymentRunItem",
                column: "PaymentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRunItem_TenantId_PaymentRunId_IsExcluded",
                schema: "fin",
                table: "PaymentRunItem",
                columns: new[] { "TenantId", "PaymentRunId", "IsExcluded" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // sec.TenantIsolationPolicy is SCHEMABINDING and blocks dropping any
            // tenant-scoped table; db/scripts/020 rebuilds it on the next start.
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM sys.security_policies WHERE name = N'TenantIsolationPolicy') " +
                "DROP SECURITY POLICY sec.TenantIsolationPolicy;");

            migrationBuilder.DropTable(
                name: "PaymentMethod",
                schema: "cfg");

            migrationBuilder.DropTable(
                name: "PaymentRunItem",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "PaymentRun",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "HouseBankAccount",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "HouseBank",
                schema: "fin");
        }
    }
}
