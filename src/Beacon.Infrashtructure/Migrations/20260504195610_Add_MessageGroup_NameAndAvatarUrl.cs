using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beacon.Infrashtructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_MessageGroup_NameAndAvatarUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "MessageGroups",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "MessageGroups",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "MessageGroups");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "MessageGroups");
        }
    }
}
