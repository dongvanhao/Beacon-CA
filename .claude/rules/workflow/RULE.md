# Workflow rules

## Trước khi tạo feature mới
1. Đọc CLAUDE.md để nắm conventions
2. Kiểm tra xem entity/interface đã tồn tại chưa: `grep -r "interface I" src/Beacon.Application`
3. Tạo theo thứ tự: Domain Entity → Repository Interface → EF Migration → Application Handler → Controller

## Trước khi commit
1. Chạy `dotnet build` — phải clean
2. Chạy `dotnet test --no-build` — phải pass
3. Review migration file nếu có (kiểm tra Up() và Down())
4. Không commit file: *.user, appsettings.Development.json, .vs/

## Khi gặp lỗi build
1. Đọc toàn bộ lỗi trước khi sửa
2. Nếu lỗi dependency injection: kiểm tra Program.cs → service registration
3. Nếu lỗi migration: chạy `dotnet ef migrations remove` trước khi tạo lại

## Trước khi accept code AI sinh ra — BẮTBUỘC

Với mỗi file AI tạo mới, bạn phải trả lời được 3 câu:
1. File này làm gì? (1 câu mô tả)
2. Nó phụ thuộc vào class/interface nào?
3. Nếu xóa file này, lỗi sẽ xảy ra ở đâu?

Nếu không trả lời được → yêu cầu Claude giải thích trước khi commit.