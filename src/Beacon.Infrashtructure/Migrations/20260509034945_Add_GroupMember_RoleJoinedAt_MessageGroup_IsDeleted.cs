using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beacon.Infrashtructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_GroupMember_RoleJoinedAt_MessageGroup_IsDeleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "MessageGroups",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "MessageGroups",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "JoinedAtUtc",
                table: "MessageGroupMembers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "MessageGroupMembers",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "MessageGroups");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "MessageGroups");

            migrationBuilder.DropColumn(
                name: "JoinedAtUtc",
                table: "MessageGroupMembers");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "MessageGroupMembers");
        }
    }
}
