---
name: create-admin-auth
description: Implement Admin Auth flow cho Beacon API — bao gồm IAdminRepository, LoginAdmin/LogoutAdmin handlers, JWT claims với roles+permissions, HasPermission policy, và AdminAuthController
---

# Skill: Admin Auth Flow (Login / Logout / RBAC)

## Khi nào dùng skill này

Dùng khi cần implement luồng Auth cho **Admin** (phân biệt với User flow đã có). Admin khác User ở 3 điểm:
- Đăng nhập trả thêm `roles[]` và `permissions[]` trong JWT claims
- Không có endpoint `register` — Admin chỉ được tạo qua seeding hoặc endpoint bảo vệ
- Có hệ thống RBAC: `Admin → AdminRole → Role → RolePermission → Permission`

---

## Trạng thái codebase hiện tại (đã có — KHÔNG tạo lại)

### Domain Entities (`src/Beacon.Domain/Entities/Identity/`)

**Admin** : `AuditableEntity`
- Props: `Email`, `PasswordHash`, `FullName`, `IsActive`, `LastLoginAtUtc`
- Nav: `ICollection<AdminRole> AdminRoles`, `ICollection<RefreshTokenAdmin> RefreshTokens`
- Methods: `Admin.Create(email, passwordHash, fullName)`, `RecordLogin()`, `Deactivate()`, `Activate()`, `UpdatePassword(hash)`

**Role** : `AuditableEntity`
- Props: `Name` (e.g. "SuperAdmin"), `Description`, `IsActive`
- Nav: `ICollection<AdminRole> AdminRoles`, `ICollection<RolePermission> RolePermissions`
- Methods: `Role.Create(name, description?)`, `Deactivate()`, `Activate()`

**Permission** : `BaseEntity`
- Props: `Name` (e.g. "users:read"), `Description`, `Group` (e.g. "Users")
- Nav: `ICollection<RolePermission> RolePermissions`
- Methods: `Permission.Create(name, description?, group?)`

**AdminRole** (junction Admin ↔ Role)
- Props: `AdminId`, `RoleId`, `AssignedAtUtc`
- Nav: `Admin`, `Role`
- Methods: `AdminRole.Create(adminId, roleId)`

**RolePermission** (junction Role ↔ Permission)
- Props: `RoleId`, `PermissionId`
- Nav: `Role`, `Permission`
- Methods: `RolePermission.Create(roleId, permissionId)`

**RefreshTokenAdmin** : `AuditableEntity`
- Props: `AdminId`, `Token`, `ExpiresAtUtc`, `RevokedAtUtc`, `CreatedByIp`, `RevokedByIp`, `ReplacedByToken`
- Nav: `Admin`
- Methods: `RefreshTokenAdmin.Create(adminId, token, expiresAtUtc, createdByIp?)`, `Revoke(ip?, replacedByToken?)`
- Computed: `IsExpired`, `IsRevoked`, `IsActive`

### IJwtService (đã có — cần MỞ RỘNG)

File: `src/Beacon.Application/Common/Interfaces/IService/IJwtService.cs`

```csharp
// Hiện có (KHÔNG xóa):
(string Token, DateTime ExpiresAt) GenerateAccessToken(User user);
(string Token, DateTime ExpiresAt) GenerateRefreshToken();

// Cần THÊM overload cho Admin:
(string Token, DateTime ExpiresAt) GenerateAdminAccessToken(Admin admin, IEnumerable<string> permissions);
```

### Error Codes hiện có (`Beacon.Shared.Constants.ErrorCodes.Identity`)
```csharp
ErrorCodes.Identity.USER_NOT_FOUND
ErrorCodes.Identity.EMAIL_ALREADY_EXISTS
ErrorCodes.Identity.INVALID_CREDENTIALS
ErrorCodes.Identity.TOKEN_EXPIRED
ErrorCodes.Identity.TOKEN_INVALID
ErrorCodes.Identity.ACCOUNT_INACTIVE
```

---

## Thứ tự tạo (BẮT BUỘC)

```
1. ErrorCodes.Identity      → thêm constants cho Admin vào ErrorCodes.cs
2. IAdminRepository         → src/Beacon.Domain/IRepository/
3. IJwtService              → thêm GenerateAdminAccessToken overload
4. JwtService               → implement overload mới (thêm role/permission claims)
5. DTOs                     → AdminLoginRequest, AdminAuthResponse
6. Validator                → AdminLoginRequestValidator
7. Commands + Handlers      → LoginAdminCommand, LogoutAdminCommand
8. AdminRepository          → src/Beacon.Infrashtructure/Repository/Identity/
9. HasPermission policy     → src/Beacon.Api/Authorization/
10. DI registration         → src/Beacon.Api/Program.cs
11. AdminAuthController     → src/Beacon.Api/Controllers/
```

---

## Bước 1: Thêm ErrorCodes cho Admin

File: `src/Beacon.Shared/Constants/ErrorCodes.cs` — thêm vào class `Identity`:

```csharp
public const string ADMIN_NOT_FOUND = "ADMIN_NOT_FOUND";
public const string ADMIN_INACTIVE = "ADMIN_INACTIVE";
public const string ADMIN_TOKEN_INVALID = "ADMIN_TOKEN_INVALID";
```

---

## Bước 2: IAdminRepository

`src/Beacon.Domain/IRepository/IAdminRepository.cs`

```csharp
using Beacon.Domain.Entities.Identity;

namespace Beacon.Domain.IRepository;

public interface IAdminRepository
{
    Task<Admin?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Admin?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<Admin?> GetByEmailWithRolesAsync(string email, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(Admin admin, CancellationToken ct = default);
    Task AddRefreshTokenAsync(RefreshTokenAdmin token, CancellationToken ct = default);
    Task<RefreshTokenAdmin?> GetActiveRefreshTokenAsync(string token, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

> `GetByEmailWithRolesAsync` load kèm `AdminRoles → Role → RolePermissions → Permission` — cần thiết để extract permissions khi tạo JWT.

---

## Bước 3: Mở rộng IJwtService

File: `src/Beacon.Application/Common/Interfaces/IService/IJwtService.cs`

> `User` và `Admin` đều thuộc namespace `Beacon.Domain.Entities.Identity` — một `using` duy nhất cover cả hai, không cần thêm.

```csharp
using Beacon.Domain.Entities.Identity;

namespace Beacon.Application.Common.Interfaces.IService;

public interface IJwtService
{
    (string Token, DateTime ExpiresAt) GenerateAccessToken(User user);
    (string Token, DateTime ExpiresAt) GenerateAdminAccessToken(Admin admin, IEnumerable<string> permissions);
    (string Token, DateTime ExpiresAt) GenerateRefreshToken();
}
```

---

## Bước 4: Implement GenerateAdminAccessToken trong JwtService

File: `src/Beacon.Infrashtructure/Services/JwtService.cs` — thêm method vào class hiện có:

```csharp
public (string Token, DateTime ExpiresAt) GenerateAdminAccessToken(Admin admin, IEnumerable<string> permissions)
{
    var jwtSettings = _configuration.GetSection("JwtSettings");
    var secretKey = jwtSettings["SecretKey"]!;
    var issuer = jwtSettings["Issuer"]!;
    var audience = jwtSettings["Audience"]!;
    var expiryMinutes = int.Parse(jwtSettings["ExpiryMinutes"] ?? "15");

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
        new(JwtRegisteredClaimNames.Email, admin.Email),
        new(ClaimTypes.NameIdentifier, admin.Id.ToString()),
        new(ClaimTypes.Email, admin.Email),
        new("actor", "admin"),                            // phân biệt Admin vs User token
        new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };

    // Gắn tất cả permissions vào claims
    foreach (var permission in permissions)
        claims.Add(new Claim("permission", permission));

    var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);
    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        expires: expiresAt,
        signingCredentials: credentials);

    return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
}
```

> Claim `"actor": "admin"` giúp phân biệt token của Admin với token của User trong middleware — không dùng `ClaimTypes.Role` vì Admin dùng permission-based, không phải role-based check trực tiếp.

---

## Bước 5: DTOs

`src/Beacon.Application/Features/Identity/Dtos/AdminLoginRequest.cs`
```csharp
namespace Beacon.Application.Features.Identity.Dtos;

public class AdminLoginRequest
{
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
}
```

`src/Beacon.Application/Features/Identity/Dtos/AdminAuthResponse.cs`
```csharp
namespace Beacon.Application.Features.Identity.Dtos;

public class AdminAuthResponse
{
    public Guid AdminId { get; set; }
    public string Email { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string AccessToken { get; set; } = default!;
    public string RefreshToken { get; set; } = default!;
    public DateTime AccessTokenExpiresAt { get; set; }
    public IEnumerable<string> Permissions { get; set; } = [];
}
```

`src/Beacon.Application/Features/Identity/Dtos/AdminLogoutRequest.cs`
```csharp
namespace Beacon.Application.Features.Identity.Dtos;

public class AdminLogoutRequest
{
    public string RefreshToken { get; set; } = default!;
}
```

---

## Bước 6: Validator

`src/Beacon.Application/Features/Identity/Validators/AdminLoginRequestValidator.cs`
```csharp
using Beacon.Application.Features.Identity.Dtos;
using FluentValidation;

namespace Beacon.Application.Features.Identity.Validators;

public class AdminLoginRequestValidator : AbstractValidator<AdminLoginRequest>
{
    public AdminLoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
```

> Auto-discovered qua `AddValidatorsFromAssembly` — KHÔNG đăng ký thủ công.

---

## Bước 7: Commands + Handlers

### LoginAdminCommand

`src/Beacon.Application/Features/Identity/Commands/LoginAdminCommand.cs`
```csharp
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public record LoginAdminCommand(AdminLoginRequest Request) : IRequest<Result<AdminAuthResponse>>;
```

`src/Beacon.Application/Features/Identity/Commands/LoginAdminCommandHandler.cs`
```csharp
using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public class LoginAdminCommandHandler(
    IAdminRepository adminRepository,
    IJwtService jwtService) : IRequestHandler<LoginAdminCommand, Result<AdminAuthResponse>>
{
    public async Task<Result<AdminAuthResponse>> Handle(LoginAdminCommand command, CancellationToken ct)
    {
        var req = command.Request;

        // 1. Tìm Admin kèm đầy đủ Roles → Permissions
        var admin = await adminRepository.GetByEmailWithRolesAsync(req.Email, ct);
        if (admin is null)
            return Result<AdminAuthResponse>.Failure(
                Error.Unauthorized(ErrorCodes.Identity.INVALID_CREDENTIALS, "Invalid email or password."));

        // 2. Kiểm tra trạng thái tài khoản
        if (!admin.IsActive)
            return Result<AdminAuthResponse>.Failure(
                Error.Unauthorized(ErrorCodes.Identity.ADMIN_INACTIVE, "Admin account is inactive."));

        // 3. Xác thực mật khẩu
        if (!BCrypt.Net.BCrypt.Verify(req.Password, admin.PasswordHash))
            return Result<AdminAuthResponse>.Failure(
                Error.Unauthorized(ErrorCodes.Identity.INVALID_CREDENTIALS, "Invalid email or password."));

        // 4. Ghi nhận thời điểm đăng nhập
        admin.RecordLogin();

        // 5. Thu thập tất cả permissions từ tất cả roles
        var permissions = admin.AdminRoles
            .Where(ar => ar.Role.IsActive)
            .SelectMany(ar => ar.Role.RolePermissions)
            .Select(rp => rp.Permission.Name)
            .Distinct()
            .ToList();

        // 6. Sinh tokens
        var (accessToken, accessTokenExpiresAt) = jwtService.GenerateAdminAccessToken(admin, permissions);
        var (refreshTokenValue, refreshTokenExpiresAt) = jwtService.GenerateRefreshToken();
        var refreshTokenEntity = RefreshTokenAdmin.Create(
            adminId: admin.Id,
            token: refreshTokenValue,
            expiresAtUtc: refreshTokenExpiresAt);

        await adminRepository.AddRefreshTokenAsync(refreshTokenEntity, ct);
        await adminRepository.SaveChangesAsync(ct);

        return Result<AdminAuthResponse>.Success(new AdminAuthResponse
        {
            AdminId = admin.Id,
            Email = admin.Email,
            FullName = admin.FullName,
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            AccessTokenExpiresAt = accessTokenExpiresAt,
            Permissions = permissions
        });
    }
}
```

### LogoutAdminCommand

`src/Beacon.Application/Features/Identity/Commands/LogoutAdminCommand.cs`
```csharp
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public record LogoutAdminCommand(string RefreshToken) : IRequest<Result>;
```

`src/Beacon.Application/Features/Identity/Commands/LogoutAdminCommandHandler.cs`
```csharp
using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;   // cần thiết cho Result và Error
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public class LogoutAdminCommandHandler(IAdminRepository adminRepository) : IRequestHandler<LogoutAdminCommand, Result>
{
    public async Task<Result> Handle(LogoutAdminCommand command, CancellationToken ct)
    {
        var token = await adminRepository.GetActiveRefreshTokenAsync(command.RefreshToken, ct);
        if (token is null)
            return Result.Failure(
                Error.NotFound(ErrorCodes.Identity.ADMIN_TOKEN_INVALID, "Refresh token not found or already revoked."));

        token.Revoke();
        await adminRepository.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

---

## Bước 8: AdminRepository Implementation

`src/Beacon.Infrashtructure/Repository/Identity/AdminRepository.cs`
```csharp
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository;
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Identity;

public class AdminRepository(AppDbContext context) : IAdminRepository
{
    public async Task<Admin?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Admins.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<Admin?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await context.Admins
            .FirstOrDefaultAsync(a => a.Email == email.ToLowerInvariant(), ct);

    public async Task<Admin?> GetByEmailWithRolesAsync(string email, CancellationToken ct = default)
        => await context.Admins
            .Include(a => a.AdminRoles)
                .ThenInclude(ar => ar.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(a => a.Email == email.ToLowerInvariant(), ct);

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
        => await context.Admins.AnyAsync(a => a.Email == email.ToLowerInvariant(), ct);

    public async Task AddAsync(Admin admin, CancellationToken ct = default)
        => await context.Admins.AddAsync(admin, ct);

    public async Task AddRefreshTokenAsync(RefreshTokenAdmin token, CancellationToken ct = default)
        => await context.RefreshTokenAdmins.AddAsync(token, ct);

    public async Task<RefreshTokenAdmin?> GetActiveRefreshTokenAsync(string token, CancellationToken ct = default)
        => await context.RefreshTokenAdmins
            .FirstOrDefaultAsync(rt => rt.Token == token
                && rt.RevokedAtUtc == null
                && rt.ExpiresAtUtc > DateTime.UtcNow, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
```

> ⚠️ `Infrashtructure` và `Presistence` là tên thực tế của project — **typo cố ý, KHÔNG sửa**. Xác nhận `context.Admins` và `context.RefreshTokenAdmins` đã có trong `AppDbContext`. Nếu thiếu — thêm `DbSet` trước khi build.

---

## Bước 9: HasPermission Authorization Policy

### PermissionRequirement

`src/Beacon.Api/Authorization/PermissionRequirement.cs`
```csharp
using Microsoft.AspNetCore.Authorization;

namespace Beacon.Api.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
```

### PermissionAuthorizationHandler

`src/Beacon.Api/Authorization/PermissionAuthorizationHandler.cs`
```csharp
using Microsoft.AspNetCore.Authorization;

namespace Beacon.Api.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var hasPermission = context.User.Claims
            .Any(c => c.Type == "permission" && c.Value == requirement.Permission);

        if (hasPermission)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
```

### HasPermissionAttribute

`src/Beacon.Api/Authorization/HasPermissionAttribute.cs`
```csharp
using Microsoft.AspNetCore.Authorization;

namespace Beacon.Api.Authorization;

public class HasPermissionAttribute(string permission) : AuthorizeAttribute(policy: permission)
{
}
```

### AdminOnlyAttribute (ngăn User token gọi Admin endpoint)

`src/Beacon.Api/Authorization/AdminOnlyAttribute.cs`
```csharp
using Microsoft.AspNetCore.Authorization;

namespace Beacon.Api.Authorization;

/// <summary>
/// Chỉ cho phép token có claim "actor" = "admin".
/// Dùng cho tất cả Admin endpoint thay vì [Authorize] thuần.
/// </summary>
public class AdminOnlyAttribute() : AuthorizeAttribute(policy: "AdminOnly")
{
}
```

> Sau bước DI, dùng `[AdminOnly]` thay cho `[Authorize]` trên mọi endpoint Admin để chặn User token ngay cả khi hợp lệ. Kết hợp với `[HasPermission]` khi cần check permission cụ thể hơn.

---

## Bước 10: DI Registration

Thêm vào `src/Beacon.Api/Program.cs`:

```csharp
// Admin Auth
builder.Services.AddScoped<IAdminRepository, AdminRepository>();

// Authorization handler (Singleton vì stateless)
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

// Đăng ký policies
// LƯU Ý: ASP.NET Core dùng Options pattern — gọi AddAuthorization nhiều lần là ADDITIVE,
// không override. Tuy nhiên nên tập trung vào một block để dễ quản lý.
// Nếu đã có AddAuthorization trong file — MỞ RỘNG cùng block đó, KHÔNG tạo block mới.
builder.Services.AddAuthorization(options =>
{
    // Policy phân biệt Admin vs User token (kiểm tra claim "actor")
    options.AddPolicy("AdminOnly", p => p.RequireClaim("actor", "admin"));

    // Permission policies — phải khớp tên với dữ liệu seed
    options.AddPolicy("users:read",   p => p.AddRequirements(new PermissionRequirement("users:read")));
    options.AddPolicy("users:write",  p => p.AddRequirements(new PermissionRequirement("users:write")));
    options.AddPolicy("users:delete", p => p.AddRequirements(new PermissionRequirement("users:delete")));
    options.AddPolicy("admins:manage",p => p.AddRequirements(new PermissionRequirement("admins:manage")));
    options.AddPolicy("roles:manage", p => p.AddRequirements(new PermissionRequirement("roles:manage")));
    options.AddPolicy("safety:read",  p => p.AddRequirements(new PermissionRequirement("safety:read")));
    options.AddPolicy("safety:write", p => p.AddRequirements(new PermissionRequirement("safety:write")));
});
```

> Danh sách permissions phải **khớp với dữ liệu seed** trong DB (bước sau). Thêm policy mới mỗi khi thêm permission mới.

---

## Bước 11: AdminAuthController

`src/Beacon.Api/Controllers/AdminAuthController.cs`
```csharp
using Beacon.Api.Authorization;
using Beacon.Application.Features.Identity.Commands;
using Beacon.Application.Features.Identity.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers;

[Route("api/v1/admin/auth")]
public class AdminAuthController(IMediator mediator) : BaseController
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] AdminLoginRequest request, CancellationToken ct)
        => HandleResult(await mediator.Send(new LoginAdminCommand(request), ct));

    [HttpPost("logout")]
    [AdminOnly]  // dùng AdminOnly thay [Authorize] — chặn User token ngay cả khi hợp lệ
    public async Task<IActionResult> Logout([FromBody] AdminLogoutRequest request, CancellationToken ct)
        => HandleResult(await mediator.Send(new LogoutAdminCommand(request.RefreshToken), ct));
}
```

---

## Data Seeding (làm sau khi auth xong)

Permissions nên seed từ code, không cho Admin tạo tuỳ ý. Thêm vào `AppDbContext` hoặc tạo seeder riêng:

```csharp
// Danh sách permissions chuẩn
var permissions = new[]
{
    Permission.Create("users:read",    "View user list",           "Users"),
    Permission.Create("users:write",   "Create/update users",      "Users"),
    Permission.Create("users:delete",  "Delete users",             "Users"),
    Permission.Create("admins:manage", "Manage admin accounts",    "Admin"),
    Permission.Create("roles:manage",  "Manage roles & permissions","Admin"),
    Permission.Create("safety:read",   "View safety records",      "Safety"),
    Permission.Create("safety:write",  "Modify safety records",    "Safety"),
};
```

---

## Endpoints tổng hợp sau khi xong

```
POST /api/v1/admin/auth/login    [AllowAnonymous]  ← email + password → tokens + permissions[]
POST /api/v1/admin/auth/logout   [Authorize]       ← refreshToken → revoke
```

---

## Gotchas (dễ quên)

- `GetByEmailWithRolesAsync` phải dùng 4 cấp `Include/ThenInclude` để load đủ chain: `AdminRoles → Role → RolePermissions → Permission`. Thiếu một cấp → `permissions` list sẽ rỗng, JWT không có claim.
- Claim `"actor": "admin"` trong JWT giúp phân biệt token Admin vs User — hữu ích khi một endpoint cần reject User token ngay cả khi họ có cùng permission name.
- `AddAuthorization` policy name phải **khớp chính xác** với `HasPermissionAttribute` constructor argument — case-sensitive.
- `context.RefreshTokenAdmins` DbSet phải tồn tại trong `AppDbContext` — kiểm tra trước khi build.
- `PermissionAuthorizationHandler` đăng ký là **Singleton** (không phải Scoped) vì nó stateless.
- Password Admin không được reset qua endpoint public — chỉ qua endpoint bảo vệ `[HasPermission("admins:manage")]`.

---

## Kiểm soát đầu ra (BẮT BUỘC trước khi commit)

Với mỗi file AI tạo ra, trả lời 3 câu:

| File | Làm gì | Phụ thuộc vào | Xóa → lỗi ở đâu |
|---|---|---|---|
| `IAdminRepository.cs` | Contract truy vấn Admin và RefreshTokenAdmin | Domain entities | Build error tại Handler + AdminRepository |
| `AdminRepository.cs` | EF Core query Admin kèm Include chain sâu | `AppDbContext`, `IAdminRepository` | Runtime DI error |
| `LoginAdminCommandHandler.cs` | Verify creds → extract permissions → sinh JWT | `IAdminRepository`, `IJwtService` | Login endpoint trả 500 |
| `LogoutAdminCommandHandler.cs` | Revoke RefreshTokenAdmin | `IAdminRepository` | Logout endpoint trả 500 |
| `PermissionAuthorizationHandler.cs` | Đọc claim "permission" trong JWT, so sánh với policy | `PermissionRequirement` | `[HasPermission(...)]` luôn trả 403 |
| `HasPermissionAttribute.cs` | Cú pháp ngắn thay `[Authorize(Policy="...")]` | `PermissionRequirement` tên policy | Build/runtime nếu policy chưa đăng ký |
| `AdminAuthController.cs` | HTTP layer cho Admin auth | `IMediator`, commands | 404 Not Found cho các route |

---

## Sau khi xong — bước tiếp theo

```bash
# 1. Build — phải clean
dotnet build

# 2. Viết unit test
/write-unit-test
# Handlers cần test: LoginAdminCommandHandler, LogoutAdminCommandHandler

# 3. Chạy test
dotnet test --no-build

# 4. Commit
# feat(identity): add admin login/logout with RBAC permission claims
```

Bước tiếp theo sau skill này: implement **Admin Management endpoints** (tạo Admin, gán Role, quản lý Permission) với `[HasPermission("admins:manage")]`.
