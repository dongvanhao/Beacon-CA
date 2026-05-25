using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beacon.Infrashtructure.Migrations
{
    /// <inheritdoc />
    public partial class Enable_AlertIncident_And_EmergencyContact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AlertIncident_DailySafetyRecords_DailySafetyRecordId",
                table: "AlertIncident");

            migrationBuilder.DropForeignKey(
                name: "FK_AlertIncident_Users_UserId",
                table: "AlertIncident");

            migrationBuilder.DropForeignKey(
                name: "FK_EmergencyContact_Users_UserId",
                table: "EmergencyContact");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationDelivery_AlertIncident_AlertIncidentId",
                table: "NotificationDelivery");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationDelivery_EmergencyContact_EmergencyContactId",
                table: "NotificationDelivery");

            migrationBuilder.DropPrimaryKey(
                name: "PK_EmergencyContact",
                table: "EmergencyContact");

            migrationBuilder.DropIndex(
                name: "IX_EmergencyContact_UserId",
                table: "EmergencyContact");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AlertIncident",
                table: "AlertIncident");

            migrationBuilder.RenameTable(
                name: "EmergencyContact",
                newName: "EmergencyContacts");

            migrationBuilder.RenameTable(
                name: "AlertIncident",
                newName: "AlertIncidents");

            migrationBuilder.RenameIndex(
                name: "IX_AlertIncident_UserId",
                table: "AlertIncidents",
                newName: "IX_AlertIncidents_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_AlertIncident_DailySafetyRecordId",
                table: "AlertIncidents",
                newName: "IX_AlertIncidents_DailySafetyRecordId");

            migrationBuilder.AlterColumn<string>(
                name: "Relationship",
                table: "EmergencyContacts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "EmergencyContacts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ContactValue",
                table: "EmergencyContacts",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "AlertIncidents",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FailureReason",
                table: "AlertIncidents",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_EmergencyContacts",
                table: "EmergencyContacts",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AlertIncidents",
                table: "AlertIncidents",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyContacts_UserId_ContactValue_ChannelType",
                table: "EmergencyContacts",
                columns: new[] { "UserId", "ContactValue", "ChannelType" });

            migrationBuilder.AddForeignKey(
                name: "FK_AlertIncidents_DailySafetyRecords_DailySafetyRecordId",
                table: "AlertIncidents",
                column: "DailySafetyRecordId",
                principalTable: "DailySafetyRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AlertIncidents_Users_UserId",
                table: "AlertIncidents",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmergencyContacts_Users_UserId",
                table: "EmergencyContacts",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationDelivery_AlertIncidents_AlertIncidentId",
                table: "NotificationDelivery",
                column: "AlertIncidentId",
                principalTable: "AlertIncidents",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationDelivery_EmergencyContacts_EmergencyContactId",
                table: "NotificationDelivery",
                column: "EmergencyContactId",
                principalTable: "EmergencyContacts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AlertIncidents_DailySafetyRecords_DailySafetyRecordId",
                table: "AlertIncidents");

            migrationBuilder.DropForeignKey(
                name: "FK_AlertIncidents_Users_UserId",
                table: "AlertIncidents");

            migrationBuilder.DropForeignKey(
                name: "FK_EmergencyContacts_Users_UserId",
                table: "EmergencyContacts");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationDelivery_AlertIncidents_AlertIncidentId",
                table: "NotificationDelivery");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationDelivery_EmergencyContacts_EmergencyContactId",
                table: "NotificationDelivery");

            migrationBuilder.DropPrimaryKey(
                name: "PK_EmergencyContacts",
                table: "EmergencyContacts");

            migrationBuilder.DropIndex(
                name: "IX_EmergencyContacts_UserId_ContactValue_ChannelType",
                table: "EmergencyContacts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AlertIncidents",
                table: "AlertIncidents");

            migrationBuilder.RenameTable(
                name: "EmergencyContacts",
                newName: "EmergencyContact");

            migrationBuilder.RenameTable(
                name: "AlertIncidents",
                newName: "AlertIncident");

            migrationBuilder.RenameIndex(
                name: "IX_AlertIncidents_UserId",
                table: "AlertIncident",
                newName: "IX_AlertIncident_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_AlertIncidents_DailySafetyRecordId",
                table: "AlertIncident",
                newName: "IX_AlertIncident_DailySafetyRecordId");

            migrationBuilder.AlterColumn<string>(
                name: "Relationship",
                table: "EmergencyContact",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "EmergencyContact",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "ContactValue",
                table: "EmergencyContact",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "AlertIncident",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FailureReason",
                table: "AlertIncident",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_EmergencyContact",
                table: "EmergencyContact",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AlertIncident",
                table: "AlertIncident",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyContact_UserId",
                table: "EmergencyContact",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AlertIncident_DailySafetyRecords_DailySafetyRecordId",
                table: "AlertIncident",
                column: "DailySafetyRecordId",
                principalTable: "DailySafetyRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AlertIncident_Users_UserId",
                table: "AlertIncident",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmergencyContact_Users_UserId",
                table: "EmergencyContact",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationDelivery_AlertIncident_AlertIncidentId",
                table: "NotificationDelivery",
                column: "AlertIncidentId",
                principalTable: "AlertIncident",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationDelivery_EmergencyContact_EmergencyContactId",
                table: "NotificationDelivery",
                column: "EmergencyContactId",
                principalTable: "EmergencyContact",
                principalColumn: "Id");
        }
    }
}
