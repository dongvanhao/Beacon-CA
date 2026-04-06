# 06. Hướng dẫn Seed dữ liệu bằng EF Migration

## 1. Mục tiêu
Tài liệu này hướng dẫn cách seed dữ liệu theo hướng one-time bằng EF Migration:
- Tạo seed mới
- Sửa seed cũ
- Thêm seed cho dữ liệu mới
- Chạy lệnh apply seed
- Rollback khi cần

Áp dụng cho cấu trúc hiện tại của dự án Beacon-CA.

---

## 2. Nguyên tắc chuẩn
1. Seed dữ liệu reference bằng migration, không seed runtime trong `Program.cs`.
2. Dữ liệu seed phải được version hóa theo migration.
3. Không chỉnh dữ liệu trực tiếp trên DB production nếu dữ liệu đó do migration quản lý.
4. Mọi thay đổi seed (thêm/sửa/xóa) đều tạo migration mới.

---

## 3. Vị trí file liên quan
- Migration: `src/Beacon.Infrashtructure/Migrations/`
- DbContext: `src/Beacon.Infrashtructure/Presistence/AppDbContext.cs`
- Startup project để chạy EF: `src/Beacon.Api/`

Ví dụ migration seed hiện tại:
- `src/Beacon.Infrashtructure/Migrations/20260406131942_SeedIdentityReferenceData.cs`

---

## 4. Tạo seed mới (lần đầu)
### Bước 1: Tạo migration
Chạy từ thư mục `src`:

```powershell
cd C:\Users\Admin\Desktop\Beacon\Beacon-CA\src

dotnet ef migrations add SeedIdentityReferenceData --project Beacon.Infrashtructure --startup-project Beacon.Api --context AppDbContext
```

### Bước 2: Viết dữ liệu seed trong migration
Trong method `Up(...)` dùng:
- `InsertData(...)`
- Có thể thêm `DeleteData(...)` trước nếu cần làm sạch dữ liệu cũ theo key

Trong method `Down(...)` dùng:
- `DeleteData(...)` để rollback dữ liệu đã seed

### Bước 3: Apply migration

```powershell
dotnet ef database update --project Beacon.Infrashtructure --startup-project Beacon.Api --context AppDbContext
```

---

## 5. Sửa seed đã có
Không sửa migration cũ nếu migration đó đã apply ở môi trường dùng chung.

Cách đúng:
1. Tạo migration mới, ví dụ:

```powershell
dotnet ef migrations add UpdateIdentitySeedV2 --project Beacon.Infrashtructure --startup-project Beacon.Api --context AppDbContext
```

2. Trong `Up(...)` dùng:
- `UpdateData(...)` để sửa dữ liệu có sẵn
- `InsertData(...)` để thêm dữ liệu mới
- `DeleteData(...)` để xóa dữ liệu không còn dùng

3. Chạy update DB:

```powershell
dotnet ef database update --project Beacon.Infrashtructure --startup-project Beacon.Api --context AppDbContext
```

---

## 6. Thêm seed mới (không ảnh hưởng seed cũ)
Ví dụ thêm permission mới:
1. Tạo migration mới:

```powershell
dotnet ef migrations add AddMoreIdentityPermissions --project Beacon.Infrashtructure --startup-project Beacon.Api --context AppDbContext
```

2. Trong migration:
- `InsertData(...)` permission mới
- `InsertData(...)` role_permission map tương ứng

3. Apply:

```powershell
dotnet ef database update --project Beacon.Infrashtructure --startup-project Beacon.Api --context AppDbContext
```

---

## 7. Lệnh chạy seed chuẩn
Seed theo migration không có lệnh riêng kiểu "run seed".
Seed chạy khi bạn chạy migration update database.

Các lệnh thường dùng:

```powershell
# Xem danh sách migration
dotnet ef migrations list --project Beacon.Infrashtructure --startup-project Beacon.Api --context AppDbContext

# Apply migration mới nhất (bao gồm seed)
dotnet ef database update --project Beacon.Infrashtructure --startup-project Beacon.Api --context AppDbContext

# Update tới migration cụ thể
dotnet ef database update 20260406131942_SeedIdentityReferenceData --project Beacon.Infrashtructure --startup-project Beacon.Api --context AppDbContext
```

---

## 8. Rollback seed
Rollback về migration trước:

```powershell
dotnet ef database update 20260406131428_RemoveAdminEmailFromAdmins --project Beacon.Infrashtructure --startup-project Beacon.Api --context AppDbContext
```

Lúc này method `Down(...)` của migration seed sẽ chạy để xóa dữ liệu seed tương ứng.

---

## 9. Lỗi thường gặp
### 9.1 MSB1009 Project file does not exist
Nguyên nhân: sai thư mục hoặc sai đường dẫn `--project`.

Khuyến nghị: luôn chạy từ thư mục `src` với:
- `--project Beacon.Infrashtructure`
- `--startup-project Beacon.Api`

### 9.2 Startup thiếu EF Design package
Nếu báo thiếu `Microsoft.EntityFrameworkCore.Design`, thêm package vào startup project `Beacon.Api`.

### 9.3 Trùng khóa/unique khi InsertData
Nguyên nhân: seed id hoặc code/name đã tồn tại.

Cách xử lý:
- Dùng `DeleteData(...)` trước khi `InsertData(...)` theo key quản lý
- Hoặc đổi key/id seed mới

---

## 10. Checklist best practice
1. Seed bằng migration, không seed runtime.
2. Không chỉnh migration cũ đã apply production.
3. Mỗi lần đổi seed tạo migration mới.
4. Viết đủ cả `Up(...)` và `Down(...)`.
5. Review kỹ dữ liệu có unique key trước khi chạy update.
6. Commit code + migration cùng lúc.
