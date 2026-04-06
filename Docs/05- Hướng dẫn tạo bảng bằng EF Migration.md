# 05. Hướng dẫn tạo bảng bằng EF Migration

## 1. Mục tiêu
Tài liệu này hướng dẫn cách:
- Tạo migration từ model EF Core
- Apply migration vào database
- Xử lý các lỗi thường gặp khi chạy `dotnet ef`

Phạm vi hiện tại: nhóm bảng Identity Admin
- `admins`
- `roles`
- `permissions`
- `role_permissions`
- `admin_roles`
- `refresh_tokens_admin`

---

## 2. Vị trí code liên quan
- Entity: `src/Beacon.Domain/Entities/Identity/`
- EF Config: `src/Beacon.Infrashtructure/Presistence/Configuration/Identity/`
- DbContext: `src/Beacon.Infrashtructure/Presistence/AppDbContext.cs`
- Migration: `src/Beacon.Infrashtructure/Migrations/`

---

## 3. Điều kiện trước khi migrate
1. Cài .NET SDK đúng version của solution (`src/global.json`).
2. Có EF CLI:
```powershell
dotnet ef --version
```
3. Startup project có package `Microsoft.EntityFrameworkCore.Design`.
4. Database server đang chạy và connection string đúng.

---

## 4. Chạy migration đúng cách
Khuyến nghị chạy từ thư mục `src` để tránh lỗi đường dẫn project.

```powershell
cd C:\Users\Admin\Desktop\Beacon\Beacon-CA\src

dotnet ef migrations add InitIdentityTables --project Beacon.Infrashtructure --startup-project Beacon.Api

dotnet ef database update --project Beacon.Infrashtructure --startup-project Beacon.Api
```

Kết quả mong đợi:
- Tạo file migration trong `src/Beacon.Infrashtructure/Migrations/`
- Database có đầy đủ các bảng mới

---

## 5. Chạy khi đang đứng trong Beacon.Api
Nếu đang ở `src/Beacon.Api`, phải dùng đường dẫn tương đối đúng:

```powershell
cd C:\Users\Admin\Desktop\Beacon\Beacon-CA\src\Beacon.Api

dotnet ef migrations add InitIdentityTables --project ..\Beacon.Infrashtructure --startup-project .

dotnet ef database update --project ..\Beacon.Infrashtructure --startup-project .
```

---

## 6. Cấu hình connection string khi chạy local
EF dùng cấu hình từ startup project (`Beacon.Api`).

Bạn có thể dùng theo chuẩn ASP.NET bằng biến môi trường:
- `ConnectionStrings__DefaultConnection`

Ví dụ PowerShell:
```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost,14333;Database=BeaconDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

Sau đó chạy lệnh migration/update như phần trên.

---

## 7. Lỗi thường gặp và cách xử lý
### 7.1 MSB1009: Project file does not exist
Nguyên nhân: đứng sai thư mục và truyền `--project` sai đường dẫn.

Cách xử lý:
- Chạy từ `src` với:
  - `--project Beacon.Infrashtructure`
  - `--startup-project Beacon.Api`

### 7.2 Startup project doesn't reference Microsoft.EntityFrameworkCore.Design
Nguyên nhân: thiếu package `Microsoft.EntityFrameworkCore.Design` ở startup project.

Cách xử lý:
- Thêm package vào `src/Beacon.Api/Beacon.Api.csproj`.

### 7.3 Kết nối DB thất bại
Nguyên nhân: SQL Server chưa chạy hoặc connection string sai.

Cách xử lý:
- Kiểm tra service/container DB
- Kiểm tra host, port, user, password
- Kiểm tra biến `ConnectionStrings__DefaultConnection`

---

## 8. Kiểm tra migration đã apply
Bạn có thể kiểm tra nhanh:

```powershell
cd C:\Users\Admin\Desktop\Beacon\Beacon-CA\src

dotnet ef migrations list --project Beacon.Infrashtructure --startup-project Beacon.Api
```

Kỳ vọng thấy `InitIdentityTables` trong danh sách.

---

## 9. Quy trình chuẩn cho lần sau
1. Sửa Entity/Configuration/DbContext
2. Tạo migration mới
3. Review file migration
4. Chạy `database update`
5. Commit cả code + migration

Không chỉnh tay file `ModelSnapshot` trừ khi thật sự cần thiết.

---

## 10. Best Practice khi bỏ cột (ví dụ bỏ `email` khỏi `admins`)
### 10.1 Nguyên tắc an toàn
1. Kiểm tra toàn bộ code còn dùng field đó hay không (entity, config, service, query, dto).
2. Nếu cột có dữ liệu quan trọng, cần backup trước khi drop.
3. Tách thay đổi thành 2 bước khi cần zero-downtime:
- Bước A: ngừng ghi/đọc cột trong code.
- Bước B: deploy migration drop cột.

### 10.2 Các bước thực hiện trong Beacon
1. Xóa property `Email` khỏi entity `Admin`.
2. Xóa mapping `email` và unique index `IX_admins_email` trong `AdminConfiguration`.
3. Tạo migration mới:

```powershell
cd C:\Users\Admin\Desktop\Beacon\Beacon-CA\src
dotnet ef migrations add RemoveAdminEmailFromAdmins --project Beacon.Infrashtructure --startup-project Beacon.Api --context AppDbContext
```

4. Apply migration:

```powershell
dotnet ef database update --project Beacon.Infrashtructure --startup-project Beacon.Api --context AppDbContext
```

### 10.3 Verify sau khi migrate
1. Bảng `identity.admins` không còn cột `email`.
2. Không còn index `IX_admins_email`.
3. API chạy bình thường, không lỗi mapping entity.
