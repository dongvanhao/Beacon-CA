using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beacon.Infrashtructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Message_SequenceNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Friends_MessageGroups_MessageGroupId",
                table: "Friends");

            migrationBuilder.DropIndex(
                name: "IX_Friends_MessageGroupId",
                table: "Friends");

            migrationBuilder.DropColumn(
                name: "MessageGroupId",
                table: "Friends");

            migrationBuilder.AddColumn<string>(
                name: "ClientMessageId",
                table: "Messages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "Messages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EditedAtUtc",
                table: "Messages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Messages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplyToMessageId",
                table: "Messages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SequenceNumber",
                table: "Messages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "FriendRequests",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "MessageGroupMemberSettings",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CustomAvatarUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsMuted = table.Column<bool>(type: "bit", nullable: false),
                    LastReadMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageGroupMemberSettings", x => new { x.GroupId, x.UserId });
                    table.ForeignKey(
                        name: "FK_MessageGroupMemberSettings_MessageGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "MessageGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MessageGroupMemberSettings_Messages_LastReadMessageId",
                        column: x => x.LastReadMessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_GroupId_ClientMessageId",
                table: "Messages",
                columns: new[] { "GroupId", "ClientMessageId" },
                unique: true,
                filter: "[ClientMessageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_GroupId_SequenceNumber",
                table: "Messages",
                columns: new[] { "GroupId", "SequenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ReplyToMessageId",
                table: "Messages",
                column: "ReplyToMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageGroupMemberSettings_LastReadMessageId",
                table: "MessageGroupMemberSettings",
                column: "LastReadMessageId");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Messages_ReplyToMessageId",
                table: "Messages",
                column: "ReplyToMessageId",
                principalTable: "Messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Messages_ReplyToMessageId",
                table: "Messages");

            migrationBuilder.DropTable(
                name: "MessageGroupMemberSettings");

            migrationBuilder.DropIndex(
                name: "IX_Messages_GroupId_ClientMessageId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_GroupId_SequenceNumber",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ReplyToMessageId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ClientMessageId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "EditedAtUtc",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ReplyToMessageId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "SequenceNumber",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "FriendRequests");

            migrationBuilder.AddColumn<Guid>(
                name: "MessageGroupId",
                table: "Friends",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Friends_MessageGroupId",
                table: "Friends",
                column: "MessageGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_Friends_MessageGroups_MessageGroupId",
                table: "Friends",
                column: "MessageGroupId",
                principalTable: "MessageGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
