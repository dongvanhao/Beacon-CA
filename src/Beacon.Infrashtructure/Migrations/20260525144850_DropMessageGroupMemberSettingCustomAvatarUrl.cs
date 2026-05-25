using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beacon.Infrashtructure.Migrations
{
    /// <inheritdoc />
    public partial class DropMessageGroupMemberSettingCustomAvatarUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomAvatarUrl",
                table: "MessageGroupMemberSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomAvatarUrl",
                table: "MessageGroupMemberSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
