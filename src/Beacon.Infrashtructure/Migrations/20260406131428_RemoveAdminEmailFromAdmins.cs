using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beacon.Infrashtructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAdminEmailFromAdmins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_admins_email",
                schema: "identity",
                table: "admins");

            migrationBuilder.DropColumn(
                name: "email",
                schema: "identity",
                table: "admins");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "email",
                schema: "identity",
                table: "admins",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_admins_email",
                schema: "identity",
                table: "admins",
                column: "email",
                unique: true,
                filter: "[email] IS NOT NULL");
        }
    }
}
