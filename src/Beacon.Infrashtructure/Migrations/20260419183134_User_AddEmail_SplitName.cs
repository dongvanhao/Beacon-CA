using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beacon.Infrashtructure.Migrations
{
    /// <inheritdoc />
    public partial class User_AddEmail_SplitName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add new columns as nullable so existing rows are not blocked by NOT NULL.
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FamilyName",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GivenName",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // 2. Backfill from existing data.
            //    - Email     = "{username}@placeholder.local" (unique vì Username đã unique). User phải verify lại.
            //    - FamilyName = token đầu của FullName (theo convention họ-tên VN: họ đứng trước).
            //    - GivenName  = phần còn lại; nếu FullName chỉ có 1 từ thì GivenName = ''.
            migrationBuilder.Sql(@"
                UPDATE [Users]
                SET [Email] = LOWER([Username]) + '@placeholder.local',
                    [FamilyName] = CASE
                        WHEN CHARINDEX(' ', LTRIM(RTRIM([FullName]))) > 0
                            THEN LEFT(LTRIM(RTRIM([FullName])), CHARINDEX(' ', LTRIM(RTRIM([FullName]))) - 1)
                        ELSE LTRIM(RTRIM([FullName]))
                    END,
                    [GivenName] = CASE
                        WHEN CHARINDEX(' ', LTRIM(RTRIM([FullName]))) > 0
                            THEN LTRIM(SUBSTRING(LTRIM(RTRIM([FullName])), CHARINDEX(' ', LTRIM(RTRIM([FullName]))) + 1, LEN([FullName])))
                        ELSE ''
                    END;
            ");

            // 3. Promote to NOT NULL after backfill.
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(254)",
                oldMaxLength: 254,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FamilyName",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GivenName",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            // 4. Unique index on Email.
            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            // 5. Drop the old FullName column last.
            migrationBuilder.DropColumn(
                name: "FullName",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1. Re-add FullName as nullable so existing rows survive.
            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            // 2. Recombine FamilyName + GivenName -> FullName.
            migrationBuilder.Sql(@"
                UPDATE [Users]
                SET [FullName] = CASE
                    WHEN [GivenName] IS NULL OR LTRIM(RTRIM([GivenName])) = '' THEN [FamilyName]
                    ELSE [FamilyName] + ' ' + [GivenName]
                END;
            ");

            // 3. Promote FullName to NOT NULL.
            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            // 4. Drop the new artefacts.
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FamilyName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GivenName",
                table: "Users");
        }
    }
}
