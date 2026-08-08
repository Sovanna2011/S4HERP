using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace S4HERP.Host.Migrations
{
    /// <summary>
    /// Widens <c>PaymentMethods</c> from one character to ten, so a partner can
    /// permit more than one — SAP's ZWELS, a concatenated list of single-character
    /// codes.
    ///
    /// This is a correction, not a feature. The payment run has matched with a
    /// substring search since it was written, so the code always assumed a list
    /// while the column could hold exactly one entry. Nothing failed, because no
    /// partner had ever needed two; the first one that did got
    /// "String or binary data would be truncated" out of the seeder.
    /// </summary>
    public partial class PaymentMethodsAreAList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PaymentMethods",
                schema: "mdm",
                table: "BusinessPartnerCompanyCode",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1)",
                oldMaxLength: 1,
                oldNullable: true);
        }

        /// <summary>
        /// Narrowing back truncates any partner that permits more than one
        /// method, silently dropping every code after the first. SQL Server
        /// refuses the ALTER outright rather than truncating, so this fails loudly
        /// on a database with real data — which is the right outcome, but it does
        /// mean the rollback needs those partners cut back to one method first.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PaymentMethods",
                schema: "mdm",
                table: "BusinessPartnerCompanyCode",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldNullable: true);
        }
    }
}
