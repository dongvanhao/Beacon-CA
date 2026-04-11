---
name: add-migration
description: Tạo EF Core migration sau khi thay đổi entity hoặc EF configuration trong Beacon API
---

# Skill: Add EF Core Migration

## Điều kiện trước khi chạy

Kiểm tra đã hoàn thành:
- [ ] `DbSet<{Entity}>` đã thêm vào `AppDbContext` (`src/Beacon.Infrashtructure/Presistence/AppDbContext.cs`)
- [ ] `IEntityTypeConfiguration<{Entity}>` đã tạo trong `Presistence/Configuration/{Module}/`
- [ ] Nếu soft-delete: `HasQueryFilter` đã thêm vào `OnModelCreating` trong `AppDbContext`

## Bước 1: Tạo migration

Chạy từ **solution root** (thư mục chứa `Beacon.sln`):

```bash
dotnet ef migrations add {MigrationName} --project src/Beacon.Infrashtructure --startup-project src/Beacon.Api
```

Naming convention: `{Verb}{EntityName}` — ví dụ:
- `AddCheckinTable`
- `AddUserLastLoginColumn`
- `AddEmergencyContactSoftDelete`
- `CreateInitialSchema`

## Bước 2: Review migration file

File được tạo tại: `src/Beacon.Infrashtructure/Presistence/Migrations/`

Kiểm tra bắt buộc:
- `Up()`: có tạo đúng table/column/index không?
- `Down()`: có rollback hoàn toàn không?
- Không có thay đổi ngoài ý muốn so với model snapshot

## Bước 3: Apply migration

```bash
dotnet ef database update --project src/Beacon.Infrashtructure --startup-project src/Beacon.Api
```

## Nếu migration sai: xóa và làm lại

```bash
dotnet ef migrations remove --project src/Beacon.Infrashtructure --startup-project src/Beacon.Api
```

Sau đó sửa entity/config và lặp lại từ Bước 1.

## Xem danh sách migrations hiện có

```bash
dotnet ef migrations list --project src/Beacon.Infrashtructure --startup-project src/Beacon.Api
```

## Gotchas
- Phải chạy từ **solution root**, không phải từ trong project folder
- Nếu `AppDbContext` không resolvable (chưa đăng ký `AddDbContext` trong `Program.cs`), CLI sẽ fail
- KHÔNG sửa migration đã apply lên production/staging DB — tạo migration mới thay thế
- Cả hai flag `--project` và `--startup-project` đều **bắt buộc**
