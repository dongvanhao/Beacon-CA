using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beacon.Infrashtructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_FriendRequest_ReceiverFK_And_Indexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FriendRequests_SenderId",
                table: "FriendRequests");

            migrationBuilder.CreateIndex(
                name: "IX_FriendRequests_Peers_Status",
                table: "FriendRequests",
                columns: new[] { "SenderId", "ReceiverId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FriendRequests_Receiver_Status_CreatedAt",
                table: "FriendRequests",
                columns: new[] { "ReceiverId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FriendRequests_Sender_Status_CreatedAt",
                table: "FriendRequests",
                columns: new[] { "SenderId", "Status", "CreatedAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_FriendRequests_Users_ReceiverId",
                table: "FriendRequests",
                column: "ReceiverId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FriendRequests_Users_ReceiverId",
                table: "FriendRequests");

            migrationBuilder.DropIndex(
                name: "IX_FriendRequests_Peers_Status",
                table: "FriendRequests");

            migrationBuilder.DropIndex(
                name: "IX_FriendRequests_Receiver_Status_CreatedAt",
                table: "FriendRequests");

            migrationBuilder.DropIndex(
                name: "IX_FriendRequests_Sender_Status_CreatedAt",
                table: "FriendRequests");

            migrationBuilder.CreateIndex(
                name: "IX_FriendRequests_SenderId",
                table: "FriendRequests",
                column: "SenderId");
        }
    }
}
