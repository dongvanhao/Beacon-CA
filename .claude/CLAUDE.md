# CLAUDE.md — Beacon

Beacon là backend **.NET 8 Clean Architecture**: MediatR + FluentValidation, EF Core (SQL Server), JWT + RBAC, Result pattern, manual DTO mapping.

Chi tiết rules nằm ở `.claude/rules/`. Commands: `.claude/commands/`. Agents: `.claude/agents/`.

---

## Quick Commands

```bash
dotnet restore && dotnet build
cd src/Beacon.Api && dotnet run
dotnet test        # tests/Beacon.UnitTests | tests/Beacon.IntergrationTests

# EF Core migrations (solution root)
dotnet ef migrations add <Name> --project src/Beacon.Infrashtructure --startup-project src/Beacon.Api
dotnet ef database update       --project src/Beacon.Infrashtructure --startup-project src/Beacon.Api
```

---

## Architecture

```
Beacon.Api            Controllers, Middleware, DI wiring
Beacon.Application    MediatR handlers, DTOs, Validators, Mappers (không import EF Core)
Beacon.Domain         Entities, Enums, Repository interfaces (ZERO framework deps)
Beacon.Infrashtructure EF DbContext, Repositories, JwtService, MinIO
Beacon.Shared         Result<T>, ApiResponse<T>, Pagination, ErrorCodes
```

Dependencies inward: `Api → Application → Domain ← Infrashtructure`.

---

## Namespace Quirks (đã track — KHÔNG sửa khi chưa có ADR)

| Folder / Project | Namespace thực tế |
|---|---|
| `src/Beacon.Infrashtructure` | `Beacon.Infrashtructure` (typo) |
| `Infrashtructure/Dependencyinjection/` | `Beacon.Infrashtructure.Dependencyinjection` (chữ `i` thường) |
| `Infrashtructure/Presistence/` | `Beacon.Infrashtructure.Presistence` (typo) |
| `Domain/Entities/Settings/` | `Beacon.Domain.Entities.Setting` (không `s`) |
| `tests/Beacon.IntergrationTests` | `Beacon.IntergrationTests` (typo) |

---

## Mandatory Rules (non-negotiable)

| Category | Rule |
|---|---|
| **Result** | Handlers/services trả `Result<T>`; throw **chỉ** cho lỗi không phục hồi |
| **Layering** | Domain = zero framework; Application không import EF Core |
| **Repository** | Handler **không** đụng `DbContext` — luôn qua interface |
| **Handler** | 1 handler = 1 use case; không AggregateService god-class |
| **DI** | Đăng ký qua extension methods, **không** trực tiếp ở `Program.cs` |
| **Entity** | Chọn đúng base: `BaseEntity` / `AuditableEntity` / `SoftDeletableEntity` |
| **EF Config** | Mỗi entity có `IEntityTypeConfiguration<T>`; soft-delete filter trong `OnModelCreating` |
| **API envelope** | Response luôn `ApiResponse<T>`; controller dùng `HandleResult` / `CreatedResult` |
| **Routing** | `/api/v1/{resource}` kebab-case, `{id:guid}`; **không** dùng `api/[controller]` |
| **Security** | JWT key từ secret store; `[HasPermission]` / `[AdminOnly]`; FluentValidation mọi DTO; không log PII |
| **Testing** | Mỗi handler = 1 unit test; mỗi controller = 1 integration test; bug fix = failing test trước |

Vi phạm cần ADR riêng.

---

## Canonical Patterns

### Handler (Result + Repository + Mapper)

```csharp
public async Task<Result<UserDto>> Handle(GetUserQuery q, CancellationToken ct)
{
    var user = await _repo.GetByIdAsync(q.Id, ct);
    if (user is null)
        return Result.Failure<UserDto>(Error.NotFound(ErrorCodes.USER_NOT_FOUND, "User not found"));
    return Result.Success(_mapper.ToDto(user));
}
```

### Controller

```csharp
[Route("api/v1/users")]
[Authorize]
public class UsersController(IMediator mediator) : BaseController
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(new GetUserQuery(id), ct));
}
```

### ApiResponse<T>

```json
// success
{ "success": true,  "message": "...", "code": null,               "data": {...}, "errors": null }
// failure
{ "success": false, "message": "...", "code": "USER_NOT_FOUND",   "data": null,  "errors": ["..."] }
```

### Program.cs (extension-method only)

```csharp
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddApiAuth(builder.Configuration);
builder.Services.AddSwagger();
builder.Services.AddHealthChecking(builder.Configuration);
builder.Services.AddControllers();
```

### Manual Mapper

- 1 mapper = 1 `sealed class` tại `Application/Mappings/{Module}/{Entity}{UseCase}Mapper.cs`
- Method: `To{Dto}(entity, ...context)` — pure, sync, Singleton DI
- Cấm: business logic, I/O, async, static, extension method, generic `IMapper<,>`

```csharp
public sealed class UserAuthMapper
{
    public AuthResponse ToAuthResponse(User u, string access, string refresh, DateTime exp)
        => new() { UserId = u.Id, Username = u.Username, AccessToken = access, RefreshToken = refresh, AccessTokenExpiresAt = exp };
}
```

---

## Exception Handling

| Expected business failure | Unrecoverable error |
|---|---|
| `return Result.Failure(Error.NotFound(...))` | `throw new NotFoundException(...)` |
| BaseController tự map ErrorType → HTTP | `ExceptionHandlingMiddleware` tự map → HTTP |

Mapping: `NotFoundException → 404 · ConflictException → 409 · UnauthorizedException → 401 · ForbiddenException → 403 · ValidationException → 400 · else → 500`.

---

## Domain Modules

| Module | Status |
|---|---|
| Identity (User auth, Admin RBAC, Devices) | ✅ |
| Safety, Checkins, Notification, Settings, Storage | ✅ |
| Group, Messaging | 🚧 Scaffolding |

---

## Adding Stuff — Cheatsheet

| Adding | Modify |
|---|---|
| **Entity** | `Domain/Entities/{M}/`, `Domain/IRepository/{M}/`, `Infrashtructure/Presistence/Configuration/{M}/`, `Infrashtructure/Repository/{M}/`, `AppDbContext` DbSet, migration |
| **Use case** | `Application/Features/{M}/Commands|Queries/{UC}/{UC}Command.cs` + `Handler.cs`; validator tại `Features/{M}/Validators/{M}/` |
| **Repository impl** | Đăng ký trong `Infrashtructure/Dependencyinjection/InfrastructureServiceExtensions.cs` |
| **Mapper** | Đăng ký Singleton trong `Application/DependencyInjection/ApplicationServiceExtensions.cs` |
| **Handler / Validator** | Không cần đăng ký (auto-scan) |
| **Auth policy** | `Api/Extensions/AuthExtensions.cs` |
| **Health check** | `Api/Extensions/HealthCheckExtensions.cs` |
| **Error code** | Thêm const vào `Beacon.Shared/Constants/ErrorCodes.cs` trước khi dùng |

---

## Workflow (Slash Commands)

`/spec` → `/plan` → `/build` → `/test` → `/review` → `/deploy`  (hỗ trợ: `/debug`, `/simplify`)

---

## Open Tech Debt (flag, chưa giải quyết)

1. Namespace typos (`Infrashtructure`, `Dependencyinjection`, `Presistence`, `Setting`, `Intergration`) — chờ rename sprint
2. Mapping library: manual vs **Mapperly** — cần ADR trước khi có 100+ mapper
3. **MediatR v13 commercial** — đang pin v12; xem xét migrate sang `Mediator` (Martin Othamar)
4. Production concerns ⏳ TODO: Serilog/OTel, Rate Limiting, Redis cache, Background Jobs (Hangfire/Channels), Idempotency-Key, CI/CD
