using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace S4HERP.Host.Migrations
{
    /// <summary>
    /// Two things at once, and they belong together.
    ///
    /// <c>mdm.BusinessPartnerBankChangeRequest</c> stages a proposed change to a
    /// partner's bank details so the live record keeps holding approved values
    /// and only approved values.
    ///
    /// The nullable columns on <c>wf.WorkflowInstance</c> and <c>wf.ApprovalRule</c>
    /// are what let that object use the approval engine at all. The engine was
    /// built when every approvable thing was an accounting document, so it
    /// required a company code and a currency-denominated amount. Bank details
    /// are client-level and have no amount, and rather than feed it a company
    /// code they do not belong to and a nominal zero, the columns now admit that
    /// some approvable objects have neither.
    /// </summary>
    public partial class PartnerBankMaintenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "CurrencyId",
                schema: "wf",
                table: "WorkflowInstance",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "CompanyCodeId",
                schema: "wf",
                table: "WorkflowInstance",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                schema: "wf",
                table: "WorkflowInstance",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldPrecision: 19,
                oldScale: 4);

            migrationBuilder.AlterColumn<long>(
                name: "CurrencyId",
                schema: "wf",
                table: "ApprovalRule",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.CreateTable(
                name: "BusinessPartnerBankChangeRequest",
                schema: "mdm",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PartnerId = table.Column<long>(type: "bigint", nullable: false),
                    Operation = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PartnerBankId = table.Column<long>(type: "bigint", nullable: true),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    BankKey = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    AccountNumber = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: true),
                    AccountHolder = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Iban = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    Swift = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    PreviousValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    DecidedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    DecisionComment = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessPartnerBankChangeRequest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerBankChangeRequest_BusinessPartnerBank_PartnerBankId",
                        column: x => x.PartnerBankId,
                        principalSchema: "mdm",
                        principalTable: "BusinessPartnerBank",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessPartnerBankChangeRequest_BusinessPartner_PartnerId",
                        column: x => x.PartnerId,
                        principalSchema: "mdm",
                        principalTable: "BusinessPartner",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerBankChangeRequest_PartnerBankId",
                schema: "mdm",
                table: "BusinessPartnerBankChangeRequest",
                column: "PartnerBankId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerBankChangeRequest_PartnerId",
                schema: "mdm",
                table: "BusinessPartnerBankChangeRequest",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerBankChangeRequest_TenantId_PartnerId_Status",
                schema: "mdm",
                table: "BusinessPartnerBankChangeRequest",
                columns: new[] { "TenantId", "PartnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartnerBankChangeRequest_TenantId_RequestId",
                schema: "mdm",
                table: "BusinessPartnerBankChangeRequest",
                columns: new[] { "TenantId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_BpBankChange_OnePending",
                schema: "mdm",
                table: "BusinessPartnerBankChangeRequest",
                columns: new[] { "TenantId", "PartnerId" },
                unique: true,
                filter: "[Status] = 1");
        }

        /// <inheritdoc />
        /// <summary>
        /// Destroys the record of who approved which bank detail change, which is
        /// the evidence that the control was applied at all. Narrowing the
        /// workflow columns back also fails outright on any database that has
        /// approved a client-level object, because those rows hold the nulls the
        /// column would no longer allow — loud, and correct: there is no
        /// automatic way to give a bank change a company code it never had.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // sec.TenantIsolationPolicy is SCHEMABINDING and blocks dropping any
            // tenant-scoped table; db/scripts/020 rebuilds it on the next start.
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM sys.security_policies WHERE name = N'TenantIsolationPolicy') " +
                "DROP SECURITY POLICY sec.TenantIsolationPolicy;");

            migrationBuilder.DropTable(
                name: "BusinessPartnerBankChangeRequest",
                schema: "mdm");

            migrationBuilder.AlterColumn<long>(
                name: "CurrencyId",
                schema: "wf",
                table: "WorkflowInstance",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "CompanyCodeId",
                schema: "wf",
                table: "WorkflowInstance",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                schema: "wf",
                table: "WorkflowInstance",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldPrecision: 19,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "CurrencyId",
                schema: "wf",
                table: "ApprovalRule",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
