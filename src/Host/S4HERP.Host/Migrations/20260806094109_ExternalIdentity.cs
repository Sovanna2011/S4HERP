using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace S4HERP.Host.Migrations
{
    /// <inheritdoc />
    public partial class ExternalIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_User_Credential",
                schema: "sec",
                table: "User");

            migrationBuilder.AddColumn<string>(
                name: "ExternalIdentityProvider",
                schema: "sec",
                table: "User",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalSubjectId",
                schema: "sec",
                table: "User",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_User_ExternalIdentity",
                schema: "sec",
                table: "User",
                columns: new[] { "TenantId", "ExternalIdentityProvider", "ExternalSubjectId" },
                unique: true,
                filter: "[ExternalSubjectId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_User_Credential",
                schema: "sec",
                table: "User",
                sql: "[UserType] NOT IN (1,6) OR [PasswordHash] IS NOT NULL OR [ExternalSubjectId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_User_ExternalIdentity",
                schema: "sec",
                table: "User");

            migrationBuilder.DropCheckConstraint(
                name: "CK_User_Credential",
                schema: "sec",
                table: "User");

            migrationBuilder.DropColumn(
                name: "ExternalIdentityProvider",
                schema: "sec",
                table: "User");

            migrationBuilder.DropColumn(
                name: "ExternalSubjectId",
                schema: "sec",
                table: "User");

            migrationBuilder.AddCheckConstraint(
                name: "CK_User_Credential",
                schema: "sec",
                table: "User",
                sql: "[UserType] NOT IN (1,6) OR [PasswordHash] IS NOT NULL");
        }
    }
}
