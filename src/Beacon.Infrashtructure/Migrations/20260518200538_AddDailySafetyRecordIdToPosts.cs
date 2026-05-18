using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beacon.Infrashtructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDailySafetyRecordIdToPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DailySafetyRecordId",
                table: "Posts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Posts_DailySafetyRecordId",
                table: "Posts",
                column: "DailySafetyRecordId");

            migrationBuilder.AddForeignKey(
                name: "FK_Posts_DailySafetyRecords_DailySafetyRecordId",
                table: "Posts",
                column: "DailySafetyRecordId",
                principalTable: "DailySafetyRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Posts_DailySafetyRecords_DailySafetyRecordId",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_Posts_DailySafetyRecordId",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "DailySafetyRecordId",
                table: "Posts");
        }
    }
}
