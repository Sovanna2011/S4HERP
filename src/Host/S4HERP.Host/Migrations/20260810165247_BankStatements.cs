using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace S4HERP.Host.Migrations
{
    /// <summary>
    /// <c>fin.BankStatement</c> and <c>fin.BankStatementLine</c>: what the bank
    /// account actually did.
    ///
    /// Everything since increment 6 has described instructions this system sent
    /// and what the bank said about them. Neither says what the account holds. A
    /// transfer accepted in a pain.002 and returned a week later appears only
    /// here, and until now bank reconciliation as an activity did not exist.
    /// </summary>
    public partial class BankStatements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankStatement",
                schema: "fin",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StatementId = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: false),
                    LegalSequenceNumber = table.Column<int>(type: "int", nullable: true),
                    HouseBankAccountId = table.Column<long>(type: "bigint", nullable: false),
                    CompanyCodeId = table.Column<long>(type: "bigint", nullable: false),
                    Format = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    ClosingBalance = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrencyId = table.Column<long>(type: "bigint", nullable: false),
                    StatementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_BankStatement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankStatement_CompanyCode_CompanyCodeId",
                        column: x => x.CompanyCodeId,
                        principalSchema: "org",
                        principalTable: "CompanyCode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankStatement_Currency_CurrencyId",
                        column: x => x.CurrencyId,
                        principalSchema: "cfg",
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankStatement_HouseBankAccount_HouseBankAccountId",
                        column: x => x.HouseBankAccountId,
                        principalSchema: "fin",
                        principalTable: "HouseBankAccount",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankStatementLine",
                schema: "fin",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankStatementId = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    EntryReference = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    IsCredit = table.Column<bool>(type: "bit", nullable: false),
                    BookingDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndToEndId = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: true),
                    RemittanceInformation = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CounterpartyName = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: true),
                    BankTransactionCode = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FiscalYear = table.Column<short>(type: "smallint", nullable: true),
                    DocumentNumber = table.Column<long>(type: "bigint", nullable: true),
                    MatchMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    MatchedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    MatchedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    MatchComment = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatementLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankStatementLine_BankStatement_BankStatementId",
                        column: x => x.BankStatementId,
                        principalSchema: "fin",
                        principalTable: "BankStatement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankStatement_CompanyCodeId",
                schema: "fin",
                table: "BankStatement",
                column: "CompanyCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatement_CurrencyId",
                schema: "fin",
                table: "BankStatement",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatement_HouseBankAccountId",
                schema: "fin",
                table: "BankStatement",
                column: "HouseBankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatement_TenantId_HouseBankAccountId_StatementDate",
                schema: "fin",
                table: "BankStatement",
                columns: new[] { "TenantId", "HouseBankAccountId", "StatementDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BankStatement_TenantId_StatementId",
                schema: "fin",
                table: "BankStatement",
                columns: new[] { "TenantId", "StatementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLine_BankStatementId",
                schema: "fin",
                table: "BankStatementLine",
                column: "BankStatementId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLine_TenantId_BankStatementId_LineNumber",
                schema: "fin",
                table: "BankStatementLine",
                columns: new[] { "TenantId", "BankStatementId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLine_TenantId_EndToEndId",
                schema: "fin",
                table: "BankStatementLine",
                columns: new[] { "TenantId", "EndToEndId" });

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLine_Unreconciled",
                schema: "fin",
                table: "BankStatementLine",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        /// <summary>
        /// Destroys the imported statements and every reconciliation decision
        /// made against them — which document a movement corresponds to, and
        /// which movements somebody examined and set aside. The ledger is
        /// untouched, so the books stay right; the evidence that they were
        /// checked against the bank does not survive.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // sec.TenantIsolationPolicy is SCHEMABINDING and blocks dropping any
            // tenant-scoped table; db/scripts/020 rebuilds it on the next start.
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM sys.security_policies WHERE name = N'TenantIsolationPolicy') " +
                "DROP SECURITY POLICY sec.TenantIsolationPolicy;");

            migrationBuilder.DropTable(
                name: "BankStatementLine",
                schema: "fin");

            migrationBuilder.DropTable(
                name: "BankStatement",
                schema: "fin");
        }
    }
}
