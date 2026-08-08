using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace S4HERP.Host.Migrations
{
    /// <summary>
    /// Gives approval rules an object type, so a rule written for journal entries
    /// does not silently begin governing payment runs.
    ///
    /// No <c>sec.TenantIsolationPolicy</c> drop here, unlike the migrations that
    /// remove tenant-scoped tables: the policy is SCHEMABINDING against
    /// <c>TenantId</c>, and adding or dropping a column it does not reference is
    /// permitted. Only the table itself and the predicate's own columns are bound.
    /// </summary>
    public partial class ApprovalRuleObjectType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApprovalRule_TenantId_CompanyCodeId_DocumentTypeCode_FromAmount",
                schema: "wf",
                table: "ApprovalRule");

            migrationBuilder.AddColumn<string>(
                name: "ObjectType",
                schema: "wf",
                table: "ApprovalRule",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            // NULL means "any object type", which is the right default for a rule
            // somebody writes from now on but the wrong reading of a rule written
            // before this column existed. Every such rule was authored when journal
            // entries were the only thing approval could be started on, so leaving
            // them NULL would quietly widen them to cover payment runs — an
            // upgrade that changes what an existing control does. Name them.
            //
            // The cross-tenant flag is not optional here. sec.fn_TenantAccessPredicate
            // denies when session context is unset, so an UPDATE run without it would
            // match zero rows and report success — a silent no-op is the one failure
            // mode a data migration must not have. The prior value is restored so this
            // cannot leave a connection with wider reach than it arrived with.
            migrationBuilder.Sql("""
                DECLARE @prior bit = TRY_CAST(SESSION_CONTEXT(N'IsCrossTenant') AS bit);
                EXEC sys.sp_set_session_context @key = N'IsCrossTenant', @value = 1, @read_only = 0;

                UPDATE wf.ApprovalRule SET ObjectType = N'JournalEntry' WHERE ObjectType IS NULL;

                EXEC sys.sp_set_session_context @key = N'IsCrossTenant', @value = @prior, @read_only = 0;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRule_TenantId_ObjectType_CompanyCodeId_DocumentTypeCode_FromAmount",
                schema: "wf",
                table: "ApprovalRule",
                columns: new[] { "TenantId", "ObjectType", "CompanyCodeId", "DocumentTypeCode", "FromAmount" });
        }

        /// <summary>
        /// Dropping the column loses what it held, as any column drop does. The
        /// consequence worth knowing: on a database that already had payment-run
        /// rules, a down-then-up round trip re-runs the backfill and relabels them
        /// <c>JournalEntry</c>, because nothing left in the row says otherwise.
        /// Verified by running it. Re-seed, or set the object types by hand.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApprovalRule_TenantId_ObjectType_CompanyCodeId_DocumentTypeCode_FromAmount",
                schema: "wf",
                table: "ApprovalRule");

            migrationBuilder.DropColumn(
                name: "ObjectType",
                schema: "wf",
                table: "ApprovalRule");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRule_TenantId_CompanyCodeId_DocumentTypeCode_FromAmount",
                schema: "wf",
                table: "ApprovalRule",
                columns: new[] { "TenantId", "CompanyCodeId", "DocumentTypeCode", "FromAmount" });
        }
    }
}
