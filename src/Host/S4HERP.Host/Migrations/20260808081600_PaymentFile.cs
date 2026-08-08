using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace S4HERP.Host.Migrations
{
    /// <inheritdoc />
    public partial class PaymentFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentFile",
                schema: "fin",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PaymentRunId = table.Column<long>(type: "bigint", nullable: false),
                    MessageId = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: false),
                    Format = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TransactionCount = table.Column<int>(type: "int", nullable: false),
                    ControlSum = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrencyId = table.Column<long>(type: "bigint", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    GeneratedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DownloadCount = table.Column<int>(type: "int", nullable: false),
                    FirstDownloadedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    FirstDownloadedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LastDownloadedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    LastDownloadedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentFile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentFile_Currency_CurrencyId",
                        column: x => x.CurrencyId,
                        principalSchema: "cfg",
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentFile_PaymentRun_PaymentRunId",
                        column: x => x.PaymentRunId,
                        principalSchema: "fin",
                        principalTable: "PaymentRun",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentFile_CurrencyId",
                schema: "fin",
                table: "PaymentFile",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentFile_PaymentRunId",
                schema: "fin",
                table: "PaymentFile",
                column: "PaymentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentFile_TenantId_MessageId",
                schema: "fin",
                table: "PaymentFile",
                columns: new[] { "TenantId", "MessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentFile_TenantId_PaymentRunId",
                schema: "fin",
                table: "PaymentFile",
                columns: new[] { "TenantId", "PaymentRunId" },
                unique: true);
        }

        /// <summary>
        /// Dropping this table destroys the instructions sent to the bank, which
        /// are evidence of why money left. In an installation that has generated
        /// any, take a copy first — rolling back is a schema operation, not a
        /// retraction of the payments.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // sec.TenantIsolationPolicy is SCHEMABINDING and blocks dropping any
            // tenant-scoped table; db/scripts/020 rebuilds it on the next start.
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM sys.security_policies WHERE name = N'TenantIsolationPolicy') " +
                "DROP SECURITY POLICY sec.TenantIsolationPolicy;");

            migrationBuilder.DropTable(
                name: "PaymentFile",
                schema: "fin");
        }
    }
}
