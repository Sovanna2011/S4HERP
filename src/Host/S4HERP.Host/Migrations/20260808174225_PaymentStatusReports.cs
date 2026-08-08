using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace S4HERP.Host.Migrations
{
    /// <summary>
    /// The return leg. <c>fin.PaymentStatusReport</c> and
    /// <c>fin.PaymentStatusItem</c> record what the bank said about instructions
    /// this system sent — the half of the payment cycle that, until now, did not
    /// exist. An executed run was assumed successful, so a refused transfer left
    /// the ledger saying paid with nothing to contradict it.
    /// </summary>
    public partial class PaymentStatusReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentStatusReport",
                schema: "fin",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: false),
                    OriginalMessageId = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: false),
                    PaymentFileId = table.Column<long>(type: "bigint", nullable: false),
                    Format = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    GroupStatus = table.Column<int>(type: "int", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReportCreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ImportedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ImportedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentStatusReport", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentStatusReport_PaymentFile_PaymentFileId",
                        column: x => x.PaymentFileId,
                        principalSchema: "fin",
                        principalTable: "PaymentFile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentStatusItem",
                schema: "fin",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentStatusReportId = table.Column<long>(type: "bigint", nullable: false),
                    EndToEndId = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    ReasonText = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    PaymentDocumentNumber = table.Column<long>(type: "bigint", nullable: true),
                    FiscalYear = table.Column<short>(type: "smallint", nullable: true),
                    CompanyCodeId = table.Column<long>(type: "bigint", nullable: true),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ResolvedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ReversalDocumentNumber = table.Column<long>(type: "bigint", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentStatusItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentStatusItem_CompanyCode_CompanyCodeId",
                        column: x => x.CompanyCodeId,
                        principalSchema: "org",
                        principalTable: "CompanyCode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentStatusItem_PaymentStatusReport_PaymentStatusReportId",
                        column: x => x.PaymentStatusReportId,
                        principalSchema: "fin",
                        principalTable: "PaymentStatusReport",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentStatusItem_CompanyCodeId",
                schema: "fin",
                table: "PaymentStatusItem",
                column: "CompanyCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentStatusItem_Outstanding",
                schema: "fin",
                table: "PaymentStatusItem",
                columns: new[] { "TenantId", "Status", "IsResolved" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentStatusItem_PaymentStatusReportId",
                schema: "fin",
                table: "PaymentStatusItem",
                column: "PaymentStatusReportId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentStatusItem_TenantId_PaymentStatusReportId",
                schema: "fin",
                table: "PaymentStatusItem",
                columns: new[] { "TenantId", "PaymentStatusReportId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentStatusReport_PaymentFileId",
                schema: "fin",
                table: "PaymentStatusReport",
                column: "PaymentFileId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentStatusReport_TenantId_MessageId",
                schema: "fin",
                table: "PaymentStatusReport",
                columns: new[] { "TenantId", "MessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentStatusReport_TenantId_PaymentFileId",
                schema: "fin",
                table: "PaymentStatusReport",
                columns: new[] { "TenantId", "PaymentFileId" });
        }

        /// <inheritdoc />
        /// <summary>
        /// Discards the record of which payments the bank refused, and of which
        /// refusals were acted on. The reversals themselves survive as ordinary
        /// accounting documents, so the ledger stays right — but the reason those
        /// reversals happened is destroyed, and that is the part an auditor asks
        /// about.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // sec.TenantIsolationPolicy is SCHEMABINDING and blocks dropping any
            // tenant-scoped table; db/scripts/020 rebuilds it on the next start.
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM sys.security_policies WHERE name = N'TenantIsolationPolicy') " +
                "DROP SECURITY POLICY sec.TenantIsolationPolicy;");

            migrationBuilder.DropTable(
                name: "PaymentStatusItem",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "PaymentStatusReport",
                schema: "fin");
        }
    }
}
