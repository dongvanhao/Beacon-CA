using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beacon.Infrashtructure.Migrations
{
    /// <inheritdoc />
    public partial class Fix_User_SearchIndex_Vietnamese : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL backfill với ký tự tiếng Việt bị lỗi encoding trong một số DB driver (Docker).
            // Việc re-normalize SearchIndex được thực hiện bằng C# seeder trong Program.cs
            // (chạy ngay sau migrate, dùng StringNormalizer đã xử lý đúng Đ/đ).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
