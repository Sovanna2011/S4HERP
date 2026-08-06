using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace S4HERP.Host.Migrations
{
    /// <inheritdoc />
    public partial class ApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "wf");

            migrationBuilder.CreateTable(
                name: "ApprovalRule",
                schema: "wf",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CompanyCodeId = table.Column<long>(type: "bigint", nullable: true),
                    DocumentTypeCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    FromAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrencyId = table.Column<long>(type: "bigint", nullable: false),
                    ApproverRoleCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StepSequence = table.Column<int>(type: "int", nullable: false),
                    MakerCheckerEnforced = table.Column<bool>(type: "bit", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false, defaultValue: new DateOnly(9999, 12, 31)),
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
                    table.PrimaryKey("PK_ApprovalRule", x => x.Id);
                    table.CheckConstraint("CK_ApprovalRule_Amount", "[FromAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_ApprovalRule_CompanyCode_CompanyCodeId",
                        column: x => x.CompanyCodeId,
                        principalSchema: "org",
                        principalTable: "CompanyCode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalRule_Currency_CurrencyId",
                        column: x => x.CurrencyId,
                        principalSchema: "cfg",
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowInstance",
                schema: "wf",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ObjectType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ObjectId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CompanyCodeId = table.Column<long>(type: "bigint", nullable: false),
                    SubmittedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ObjectCreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrencyId = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowInstance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowInstance_CompanyCode_CompanyCodeId",
                        column: x => x.CompanyCodeId,
                        principalSchema: "org",
                        principalTable: "CompanyCode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowInstance_Currency_CurrencyId",
                        column: x => x.CurrencyId,
                        principalSchema: "cfg",
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStep",
                schema: "wf",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowInstanceId = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    ApproverRoleCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MakerCheckerEnforced = table.Column<bool>(type: "bit", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    DecidedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStep", x => x.Id);
                    table.CheckConstraint("CK_WorkflowStep_Decided", "[Decision] = 1 OR ([DecidedBy] IS NOT NULL AND [DecidedAtUtc] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_WorkflowStep_WorkflowInstance_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalSchema: "wf",
                        principalTable: "WorkflowInstance",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRule_CompanyCodeId",
                schema: "wf",
                table: "ApprovalRule",
                column: "CompanyCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRule_CurrencyId",
                schema: "wf",
                table: "ApprovalRule",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRule_TenantId_Code",
                schema: "wf",
                table: "ApprovalRule",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRule_TenantId_CompanyCodeId_DocumentTypeCode_FromAmount",
                schema: "wf",
                table: "ApprovalRule",
                columns: new[] { "TenantId", "CompanyCodeId", "DocumentTypeCode", "FromAmount" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstance_CompanyCodeId",
                schema: "wf",
                table: "WorkflowInstance",
                column: "CompanyCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstance_CurrencyId",
                schema: "wf",
                table: "WorkflowInstance",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstance_TenantId_Status_CompanyCodeId",
                schema: "wf",
                table: "WorkflowInstance",
                columns: new[] { "TenantId", "Status", "CompanyCodeId" });

            migrationBuilder.CreateIndex(
                name: "UX_WorkflowInstance_OnePending",
                schema: "wf",
                table: "WorkflowInstance",
                columns: new[] { "TenantId", "ObjectType", "ObjectId" },
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStep_TenantId_WorkflowInstanceId_Sequence",
                schema: "wf",
                table: "WorkflowStep",
                columns: new[] { "TenantId", "WorkflowInstanceId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStep_WorkflowInstanceId",
                schema: "wf",
                table: "WorkflowStep",
                column: "WorkflowInstanceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // sec.TenantIsolationPolicy is created WITH SCHEMABINDING, so SQL Server
            // refuses to drop any table it covers — which is every tenant-scoped
            // table, and therefore every table any migration will ever drop.
            // Verified: without this the rollback fails with error 3729.
            //
            // Dropping it here is safe because the policy is derived state, not
            // schema: db/scripts/020-row-level-security.sql rebuilds it from
            // sys.tables on every start and on every run of --migrate. Any future
            // migration that drops a tenant-scoped table needs the same two lines.
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM sys.security_policies WHERE name = N'TenantIsolationPolicy') " +
                "DROP SECURITY POLICY sec.TenantIsolationPolicy;");

            migrationBuilder.DropTable(
                name: "ApprovalRule",
                schema: "wf");

            migrationBuilder.DropTable(
                name: "WorkflowStep",
                schema: "wf");

            migrationBuilder.DropTable(
                name: "WorkflowInstance",
                schema: "wf");
        }
    }
}
