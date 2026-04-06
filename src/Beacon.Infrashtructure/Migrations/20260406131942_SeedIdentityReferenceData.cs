using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beacon.Infrashtructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedIdentityReferenceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM [identity].[role_permissions] WHERE [role_id] IN (1,2,3) AND [permission_id] IN (1,2,3);");

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permissions",
                keyColumn: "Id",
                keyValues: new object[] { 1, 2, 3 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "roles",
                keyColumn: "Id",
                keyValues: new object[] { 1, 2, 3 });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "roles",
                columns: new[] { "Id", "created_at", "description", "name" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 4, 6, 0, 0, 0, DateTimeKind.Utc), "Administrator", "ADMIN" },
                    { 2, new DateTime(2026, 4, 6, 0, 0, 0, DateTimeKind.Utc), "Moderator", "MODERATOR" },
                    { 3, new DateTime(2026, 4, 6, 0, 0, 0, DateTimeKind.Utc), "Super administrator", "SUPER_ADMIN" }
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "permissions",
                columns: new[] { "Id", "code", "created_at", "description" },
                values: new object[,]
                {
                    { 1, "USER_READ", new DateTime(2026, 4, 6, 0, 0, 0, DateTimeKind.Utc), "Read users" },
                    { 2, "USER_DELETE", new DateTime(2026, 4, 6, 0, 0, 0, DateTimeKind.Utc), "Delete users" },
                    { 3, "POST_MODERATE", new DateTime(2026, 4, 6, 0, 0, 0, DateTimeKind.Utc), "Moderate posts" }
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "role_permissions",
                columns: new[] { "Id", "permission_id", "role_id" },
                values: new object[,]
                {
                    { 1, 1, 1 },
                    { 2, 2, 1 },
                    { 3, 3, 1 },
                    { 4, 1, 2 },
                    { 5, 3, 2 },
                    { 6, 1, 3 },
                    { 7, 2, 3 },
                    { 8, 3, 3 }
                });

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "roles",
                keyColumn: "Id",
                keyValue: 1,
                column: "description",
                value: "Administrator");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_permissions",
                keyColumn: "Id",
                keyValues: new object[] { 1, 2, 3, 4, 5, 6, 7, 8 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "permissions",
                keyColumn: "Id",
                keyValues: new object[] { 1, 2, 3 });

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "roles",
                keyColumn: "Id",
                keyValues: new object[] { 1, 2, 3 });
        }
    }
}
