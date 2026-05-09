using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beacon.Infrashtructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLastSeenMessageId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LastSeenMessageId",
                table: "MessageGroupMembers",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSeenMessageId",
                table: "MessageGroupMembers");
        }
    }
}
