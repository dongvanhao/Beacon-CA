using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beacon.Infrashtructure.Migrations
{
    /// <inheritdoc />
    public partial class Sprint4_InvitedBy_AvatarFK_FriendRequestNormalize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FriendRequests_Users_ReceiverId",
                table: "FriendRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_FriendRequests_Users_SenderId",
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

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "MessageGroups");

            migrationBuilder.RenameColumn(
                name: "SenderId",
                table: "FriendRequests",
                newName: "UserId2");

            migrationBuilder.RenameColumn(
                name: "ReceiverId",
                table: "FriendRequests",
                newName: "UserId1");

            migrationBuilder.AddColumn<Guid>(
                name: "AvatarMediaObjectId",
                table: "MessageGroups",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InvitedByUserId",
                table: "MessageGroupMembers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InitiatorId",
                table: "FriendRequests",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("UPDATE [FriendRequests] SET [InitiatorId] = [UserId2]");

            migrationBuilder.CreateIndex(
                name: "IX_MessageGroups_AvatarMediaObjectId",
                table: "MessageGroups",
                column: "AvatarMediaObjectId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageGroupMembers_InvitedByUserId",
                table: "MessageGroupMembers",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FriendRequests_Initiator_Status_CreatedAt",
                table: "FriendRequests",
                columns: new[] { "InitiatorId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FriendRequests_Peers_Status",
                table: "FriendRequests",
                columns: new[] { "UserId1", "UserId2", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FriendRequests_UserId2",
                table: "FriendRequests",
                column: "UserId2");

            migrationBuilder.CreateIndex(
                name: "UX_FriendRequests_Pair_Pending",
                table: "FriendRequests",
                columns: new[] { "UserId1", "UserId2" },
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_FriendRequests_Users_InitiatorId",
                table: "FriendRequests",
                column: "InitiatorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FriendRequests_Users_UserId1",
                table: "FriendRequests",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FriendRequests_Users_UserId2",
                table: "FriendRequests",
                column: "UserId2",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MessageGroupMembers_Users_InvitedByUserId",
                table: "MessageGroupMembers",
                column: "InvitedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MessageGroups_MediaObjects_AvatarMediaObjectId",
                table: "MessageGroups",
                column: "AvatarMediaObjectId",
                principalTable: "MediaObjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FriendRequests_Users_InitiatorId",
                table: "FriendRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_FriendRequests_Users_UserId1",
                table: "FriendRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_FriendRequests_Users_UserId2",
                table: "FriendRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageGroupMembers_Users_InvitedByUserId",
                table: "MessageGroupMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageGroups_MediaObjects_AvatarMediaObjectId",
                table: "MessageGroups");

            migrationBuilder.DropIndex(
                name: "IX_MessageGroups_AvatarMediaObjectId",
                table: "MessageGroups");

            migrationBuilder.DropIndex(
                name: "IX_MessageGroupMembers_InvitedByUserId",
                table: "MessageGroupMembers");

            migrationBuilder.DropIndex(
                name: "IX_FriendRequests_Initiator_Status_CreatedAt",
                table: "FriendRequests");

            migrationBuilder.DropIndex(
                name: "IX_FriendRequests_Peers_Status",
                table: "FriendRequests");

            migrationBuilder.DropIndex(
                name: "IX_FriendRequests_UserId2",
                table: "FriendRequests");

            migrationBuilder.DropIndex(
                name: "UX_FriendRequests_Pair_Pending",
                table: "FriendRequests");

            migrationBuilder.DropColumn(
                name: "AvatarMediaObjectId",
                table: "MessageGroups");

            migrationBuilder.DropColumn(
                name: "InvitedByUserId",
                table: "MessageGroupMembers");

            migrationBuilder.DropColumn(
                name: "InitiatorId",
                table: "FriendRequests");

            migrationBuilder.RenameColumn(
                name: "UserId2",
                table: "FriendRequests",
                newName: "SenderId");

            migrationBuilder.RenameColumn(
                name: "UserId1",
                table: "FriendRequests",
                newName: "ReceiverId");

            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "MessageGroups",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

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

            migrationBuilder.AddForeignKey(
                name: "FK_FriendRequests_Users_SenderId",
                table: "FriendRequests",
                column: "SenderId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
