using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beacon.Infrashtructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_MediaObject_AvatarMediaObjectId",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MediaObject",
                table: "MediaObject");

            migrationBuilder.RenameTable(
                name: "MediaObject",
                newName: "MediaObjects");

            migrationBuilder.AlterColumn<string>(
                name: "OriginalFileName",
                table: "MediaObjects",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ObjectKey",
                table: "MediaObjects",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ETag",
                table: "MediaObjects",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                table: "MediaObjects",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ChecksumSha256",
                table: "MediaObjects",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BucketName",
                table: "MediaObjects",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "MediaObjects",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MediaType",
                table: "MediaObjects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailObjectKey",
                table: "MediaObjects",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "MediaObjects",
                type: "int",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_MediaObjects",
                table: "MediaObjects",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_MediaObjects_BucketName_ObjectKey",
                table: "MediaObjects",
                columns: new[] { "BucketName", "ObjectKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaObjects_CreatedAtUtc",
                table: "MediaObjects",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MediaObjects_UploadProviderByUserId",
                table: "MediaObjects",
                column: "UploadProviderByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_MediaObjects_AvatarMediaObjectId",
                table: "Users",
                column: "AvatarMediaObjectId",
                principalTable: "MediaObjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_MediaObjects_AvatarMediaObjectId",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MediaObjects",
                table: "MediaObjects");

            migrationBuilder.DropIndex(
                name: "IX_MediaObjects_BucketName_ObjectKey",
                table: "MediaObjects");

            migrationBuilder.DropIndex(
                name: "IX_MediaObjects_CreatedAtUtc",
                table: "MediaObjects");

            migrationBuilder.DropIndex(
                name: "IX_MediaObjects_UploadProviderByUserId",
                table: "MediaObjects");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "MediaObjects");

            migrationBuilder.DropColumn(
                name: "MediaType",
                table: "MediaObjects");

            migrationBuilder.DropColumn(
                name: "ThumbnailObjectKey",
                table: "MediaObjects");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "MediaObjects");

            migrationBuilder.RenameTable(
                name: "MediaObjects",
                newName: "MediaObject");

            migrationBuilder.AlterColumn<string>(
                name: "OriginalFileName",
                table: "MediaObject",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "ObjectKey",
                table: "MediaObject",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "ETag",
                table: "MediaObject",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                table: "MediaObject",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "ChecksumSha256",
                table: "MediaObject",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BucketName",
                table: "MediaObject",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddPrimaryKey(
                name: "PK_MediaObject",
                table: "MediaObject",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_MediaObject_AvatarMediaObjectId",
                table: "Users",
                column: "AvatarMediaObjectId",
                principalTable: "MediaObject",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
