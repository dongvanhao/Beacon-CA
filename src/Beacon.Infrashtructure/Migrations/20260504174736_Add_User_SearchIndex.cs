using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beacon.Infrashtructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_User_SearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SearchIndex",
                table: "Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Users_SearchIndex",
                table: "Users",
                column: "SearchIndex");

            // Backfill SearchIndex cho user cũ.
            // Lưu ý: SQL Server LOWER() vẫn giữ dấu tiếng Việt (không thể strip dấu bằng T-SQL thuần).
            // Các user tạo mới sau migration này sẽ có SearchIndex chuẩn qua C# StringNormalizer.
            migrationBuilder.Sql(
                "UPDATE Users SET SearchIndex = LOWER(FamilyName + ' ' + GivenName)");

            migrationBuilder.CreateIndex(
                name: "IX_FriendRequests_SenderId",
                table: "FriendRequests",
                column: "SenderId");

            migrationBuilder.AddForeignKey(
                name: "FK_FriendRequests_Users_SenderId",
                table: "FriendRequests",
                column: "SenderId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FriendRequests_Users_SenderId",
                table: "FriendRequests");

            migrationBuilder.DropIndex(
                name: "IX_Users_SearchIndex",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_FriendRequests_SenderId",
                table: "FriendRequests");

            migrationBuilder.DropColumn(
                name: "SearchIndex",
                table: "Users");
        }
    }
}
