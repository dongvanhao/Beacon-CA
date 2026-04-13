---
name: create-admin-auth
description: Tham khảo pattern Admin Auth + RBAC (roles/permissions) đã implement trong Beacon API
---

## Khi nào dùng

**Admin Auth đã implement sẵn — KHÔNG chạy lại để tạo lại.**

Dùng skill này khi:
- Thêm endpoint Admin mới và cần biết cách dùng `[HasPermission]` / `[AdminOnly]`
- Thêm permission mới vào hệ thống RBAC
- Tham khảo pattern login kèm permission claims cho entity tương tự

## Cách gọi

```
/create-admin-auth
```
Sau đó mô tả việc cần làm (thêm endpoint, thêm permission, v.v.).

## Files tham khảo (đã tồn tại — đọc trước khi viết)

```
src/Beacon.Application/Features/Identity/Commands/
├── LoginAdminCommandHandler.cs     ← mẫu: load roles → extract permissions → JWT với claims
└── LogoutAdminCommandHandler.cs    ← mẫu: revoke RefreshTokenAdmin

src/Beacon.Domain/IRepository/IAdminRepository.cs
src/Beacon.Infrashtructure/Repository/Identity/AdminRepository.cs
    └── GetByEmailWithRolesAsync    ← 4 cấp Include — quan trọng, đọc kỹ

src/Beacon.Api/Authorization/
├── PermissionRequirement.cs
├── PermissionAuthorizationHandler.cs   ← Singleton, stateless
├── HasPermissionAttribute.cs
└── AdminOnlyAttribute.cs

src/Beacon.Api/Controllers/AdminAuthController.cs
```

## Cách dùng authorization trên endpoint mới

```csharp
// Chỉ cho Admin token (chặn User token dù hợp lệ JWT)
[AdminOnly]

// Yêu cầu permission cụ thể
[HasPermission("users:read")]

// Kết hợp
[AdminOnly]
[HasPermission("admins:manage")]
```

## Thêm permission mới

```
1. Thêm policy trong Program.cs:
   options.AddPolicy("resource:action",
       p => p.AddRequirements(new PermissionRequirement("resource:action")));

2. Seed vào DB:
   Permission.Create("resource:action", "Mô tả", "Group")

3. Dùng [HasPermission("resource:action")] trên endpoint
```

## Permissions hiện có

```
users:read, users:write, users:delete
admins:manage
roles:manage
safety:read, safety:write
```

## Quyết định thiết kế không đổi

| Vấn đề | Quyết định |
|---|---|
| Phân biệt Admin/User token | Claim `"actor": "admin"` — không dùng `ClaimTypes.Role` |
| Config JWT | `IOptions<JwtSettings>` — KHÔNG inject `IConfiguration` trực tiếp |
| Load permissions | 4 cấp `Include/ThenInclude`: `AdminRoles → Role → RolePermissions → Permission` |
| Policy name | PHẢI khớp chính xác (case-sensitive) với `HasPermissionAttribute` argument |
| Handler registration | `PermissionAuthorizationHandler` → `AddSingleton` (không phải Scoped) |

## Gotchas

- Thiếu một cấp Include trong `GetByEmailWithRolesAsync` → permissions list rỗng → JWT không có claim
- `AddAuthorization` gọi nhiều lần là additive — tập trung vào 1 block trong `Program.cs`
- Password Admin chỉ reset qua endpoint `[HasPermission("admins:manage")]`, không có public endpoint

## Sau khi thêm endpoint/permission

```bash
dotnet build
/write-unit-test    # nếu thêm handler mới
dotnet test --no-build
```
