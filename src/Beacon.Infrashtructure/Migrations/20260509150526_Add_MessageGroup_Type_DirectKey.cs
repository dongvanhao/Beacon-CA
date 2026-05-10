using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beacon.Infrashtructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_MessageGroup_Type_DirectKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DirectKey",
                table: "MessageGroups",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "MessageGroups",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Direct");

            // Data migration: map IsPrivate → Type
            migrationBuilder.Sql(@"
                UPDATE MessageGroups
                SET Type = CASE WHEN IsPrivate = 1 THEN 'Direct' ELSE 'Group' END
            ");

            migrationBuilder.CreateIndex(
                name: "UX_MessageGroups_DirectKey",
                table: "MessageGroups",
                column: "DirectKey",
                unique: true,
                filter: "[DirectKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_MessageGroups_DirectKey",
                table: "MessageGroups");

            migrationBuilder.DropColumn(
                name: "DirectKey",
                table: "MessageGroups");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "MessageGroups");
        }
    }
}
