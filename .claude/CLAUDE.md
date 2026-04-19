# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build

# Run API
cd src/Beacon.Api && dotnet run

# Run all tests
dotnet test

# Run specific test project
dotnet test tests/Beacon.UnitTests
dotnet test tests/Beacon.IntergrationTests

# EF Core migrations (run from solution root)
dotnet ef migrations add <MigrationName> --project src/Beacon.Infrashtructure --startup-project src/Beacon.Api
dotnet ef database update --project src/Beacon.Infrashtructure --startup-project src/Beacon.Api
```

## Architecture

**Clean Architecture** with 5 layers, dependencies pointing inward (API → Application → Domain, Infrastructure → Domain):

```
Beacon.Api              → Controllers, Middleware, DI wiring
Beacon.Application      → Services, DTOs, Validators, feature folders
Beacon.Domain           → Entities, Enums, Repository interfaces (no framework deps)
Beacon.Infrashtructure  → EF Core DbContext, Repository impls, SQL Server
Beacon.Shared           → Result<T>, ApiResponse<T>, Guards, Pagination, Constants
```

Note: "Infrashtructure" is an intentional typo in the project name — do not rename it.

## Result Pattern (mandatory)

All service methods must return `Result` or `Result<T>` from `Beacon.Shared`. Never throw exceptions for expected business failures.

```csharp
// Service layer
public async Task<Result<UserDto>> GetUserAsync(Guid id)
{
    var user = await _repo.GetByIdAsync(id);
    if (user is null) return Result.Failure<UserDto>(Error.NotFound(ErrorCodes.USER_NOT_FOUND, "User not found"));
    return Result.Success(_userProfileMapper.ToProfileDto(user));
}

// Controller layer — use BaseController helpers
public async Task<IActionResult> GetUser(Guid id)
    => HandleResult(await _userService.GetUserAsync(id));
    // or for POST: CreatedResult("route", await _service.CreateAsync(dto))
```

## API Response Shape

All endpoints return `ApiResponse<T>` (from `Beacon.Shared`):
```json
{ "success": true, "message": "...", "code": null, "data": {}, "errors": null }
```

## Entity Base Classes

Choose the correct base for new entities:
- `BaseEntity` — just a `Guid Id`
- `AuditableEntity` — adds `CreatedAtUtc`, `UpdatedAtUtc`
- `SoftDeletableEntity` — adds `IsDeleted` with EF query filter

## Domain Modules

Feature folders exist in both `Application` and `Domain` for:
- **Identity** — users, JWT auth, refresh tokens, devices, roles (`User`, `Admin`)
  -  User auth: Register/Login/Logout/RefreshToken — `Application/Features/Identity/Commands/`
  -  Admin auth: Login/Logout với RBAC (roles + permissions) — same folder
  -  Authorization: `[HasPermission("x:y")]`, `[AdminOnly]` — `Api/Authorization/`
- **Safety** — daily records, emergency contacts, alert incidents
- **Checkins** — checkin records with media attachments
- **Notification** — delivery tracking, multi-channel (email, SMS, push)
- **Settings** — per-user safety, notification, and app preferences
- **Storage** — media objects with public/private access control
- **Group**, **Messaging** — scaffolding only, not yet implemented

## EF Core Conventions

- Configurations use Fluent API in `Infrashtructure/Presistence/Configuration/`
- Soft-delete query filters are applied in `AppDbContext.OnModelCreating`, NOT in the config class
- Register new `IEntityTypeConfiguration<T>` classes — they are auto-discovered via `ApplyConfigurationsFromAssembly`
- Always add `DbSet<T>` to `AppDbContext` when creating a new entity

## Exception Handling

Global middleware in `Beacon.Api` maps exceptions to HTTP codes:
- `NotFoundException` → 404, `ConflictException` → 409, `UnauthorizedException` → 401, `ForbiddenException` → 403, `ValidationException` → 400, unhandled → 500

Throw these custom exceptions only for unexpected/unrecoverable errors; use `Result.Failure` for all expected business failures.

## Repository Interfaces

Location: `src/Beacon.Domain/IRepository/`
- No `IRepository<T>` base interface exists yet — declare each interface directly
- Implementations go in `src/Beacon.Infrashtructure/Repository/{Module}/`

## DI Registration

Program.cs chỉ gọi extension methods — **không đăng ký trực tiếp vào Program.cs**.

| Thêm gì | Sửa file nào |
|---|---|
| Repository mới | `src/Beacon.Infrashtructure/Dependencyinjection/InfrastructureServiceExtensions.cs` |
| Handler/Validator mới | Tự động qua MediatR/FluentValidation assembly scan — không cần sửa gì |
| Mapper mới | `src/Beacon.Application/DependencyInjection/ApplicationServiceExtensions.cs` (`AddSingleton<XxxMapper>()`) |
| Authorization policy mới | `src/Beacon.Api/Extensions/AuthExtensions.cs` |
| Health check mới | `src/Beacon.Api/Extensions/HealthCheckExtensions.cs` |

```csharp
// Program.cs pattern (không sửa trừ khi thêm layer hoàn toàn mới)
builder.Services.AddInfrastructure(builder.Configuration); // DbContext, Repos, JwtService
builder.Services.AddApplication();                         // MediatR, FluentValidation
builder.Services.AddApiAuth(builder.Configuration);        // JWT Bearer, Auth, CORS
builder.Services.AddSwagger();                             // Swagger
builder.Services.AddHealthChecking(builder.Configuration); // Health checks
builder.Services.AddControllers();
```

**Lưu ý namespace:** Infrastructure folder dùng chữ thường `Dependencyinjection` (không phải `DependencyInjection`) — namespace là `Beacon.Infrashtructure.Dependencyinjection`.

## Mapping (Manual DTO Mapping — fixed decision)

**KHÔNG dùng AutoMapper / Mapster / bất kỳ mapping library nào.** Đã quyết định dùng manual DTO mapping permanent. Đừng đề xuất hay so sánh với Mapster/AutoMapper.

### Pattern bắt buộc

- 1 mapper = 1 file = 1 `sealed class` per use case
- Vị trí: `src/Beacon.Application/Mappings/{Module}/{Entity}{UseCase}Mapper.cs`
- Naming class: `{Entity}{UseCase}Mapper` (vd `UserAuthMapper`, `UserProfileMapper`, `AdminAuthMapper`)
- Naming method: `To{Dto}(entity, ...context)` — vd `ToAuthResponse`, `ToProfileDto`
- DI lifetime: **Singleton** (mapper stateless và pure)
- Đăng ký trong `ApplicationServiceExtensions.cs` → `services.AddSingleton<XxxMapper>();`
- Inject vào handler qua constructor

### Cấm trong mapper

- ❌ Business logic (validation, decision, computation) — đặt ở Domain hoặc Service
- ❌ I/O (DB, HTTP, file) — mapper phải pure & sync
- ❌ Async — `Task<TDto>` báo hiệu sai pattern
- ❌ State — mapper phải stateless để Singleton an toàn
- ❌ Static class — luôn instance class (cho phép DI dependency sau này)
- ❌ Extension method — không dùng `this Entity`, dùng instance method
- ❌ Generic `IMapper<TSource, TDest>` interface — over-engineering

### Ví dụ

```csharp
// src/Beacon.Application/Mappings/Identity/UserAuthMapper.cs
public sealed class UserAuthMapper
{
    public AuthResponse ToAuthResponse(User user, string accessToken, string refreshToken, DateTime expiresAt)
        => new()
        {
            UserId = user.Id,
            Username = user.Username,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAt = expiresAt
        };
}

// Handler inject mapper
public class LoginCommandHandler(
    IUserRepository userRepo,
    IJwtService jwtService,
    UserAuthMapper authMapper) : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    // ...
    return Result<AuthResponse>.Success(
        authMapper.ToAuthResponse(user, accessToken, refreshToken, expiresAt));
}
```

### Nested object → composition

```csharp
public sealed class CheckinDetailMapper(LocationMapper locationMapper)
{
    public CheckinDetailDto ToDetailDto(Checkin c)
        => new()
        {
            Id = c.Id,
            Location = c.Location is null ? null : locationMapper.ToDto(c.Location)
        };
}
```

### List mapping → LINQ Select

```csharp
var dtos = users.Select(userListMapper.ToListItem).ToList();
```

KHÔNG tạo `MapList()` wrapper — `Select` đã idiomatic.

## Namespace Quirk

- Folder `src/Beacon.Domain/Entities/Settings/` → namespace `Beacon.Domain.Entities.Setting` (no 's')
- Project name `Beacon.Infrashtructure` — intentional typo, do not rename

## Skills & Agents — Quick Reference

| Lệnh | Dùng khi |
|---|---|
| `/create-entity` | Chỉ cần Domain Entity + EF config, chưa cần endpoint |
| `/create-endpoint` | Full CRUD mới: entity → repository → handler → controller |
| `/add-validation` | Thêm FluentValidation cho DTO mới |
| `/add-migration` | Sau khi thay đổi entity/EF config |
| `/write-unit-test` | Sau khi implement Handler/Service mới |
| `/setup-di` | Hướng dẫn đăng ký DI hoặc tạo extension method |
| `/create-auth-handler` | Tham khảo pattern User Auth đã implement |
| `/create-admin-auth` | Tham khảo pattern Admin Auth + RBAC đã implement |

| Agent | Gọi khi |
|---|---|
| `researcher` | So sánh packages, tìm best practice, research .NET ecosystem |
| `api-reviewer` | Review convention/security/naming endpoint sau khi tạo xong |
