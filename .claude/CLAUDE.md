# CLAUDE.md — Beacon

This file provides guidance to Claude Code (claude.ai/code) when working with the Beacon codebase.

Beacon is a .NET 8 backend built on **Clean Architecture**, using **MediatR + FluentValidation**, **EF Core with SQL Server**, and **JWT-based Identity** with RBAC. The project follows strict conventions for the Result pattern, manual DTO mapping, and layered dependency injection.

---

## Quick Commands

```bash
# Dependencies & build
dotnet restore
dotnet build

# Run API
cd src/Beacon.Api && dotnet run

# Tests
dotnet test                                     # all
dotnet test tests/Beacon.UnitTests              # unit only
dotnet test tests/Beacon.IntergrationTests      # integration only

# EF Core migrations (from solution root)
dotnet ef migrations add <Name> \
  --project src/Beacon.Infrashtructure \
  --startup-project src/Beacon.Api

dotnet ef database update \
  --project src/Beacon.Infrashtructure \
  --startup-project src/Beacon.Api
```

---

## Development Workflow

All feature work follows this pipeline:

```
┌────────────────────────────────────────────────────────────────────┐
│                                                                    │
│   /spec  →  /plan  →  /build  →  /test  →  /review  →  /deploy    │
│                                                                    │
│   Define    Slice     TDD       Verify    5-axis      Ship         │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

| Phase      | Command    | Purpose                                                                 |
| ---------- | ---------- | ----------------------------------------------------------------------- |
| **Define** | `/spec`    | PRD with objectives, scope, non-goals, acceptance criteria              |
| **Plan**   | `/plan`    | Decompose into vertical slices: Entity → Repo → Handler → Controller    |
| **Build**  | `/build`   | Implement incrementally using TDD (RED → GREEN → REFACTOR)              |
| **Verify** | `/test`    | Unit + Integration tests; reproduce bugs before fixing                  |
| **Review** | `/review`  | Five-axis review: Correctness, Readability, Architecture, Security, Perf |
| **Ship**   | `/deploy`  | Migration check → build → test → staged rollout                         |

### Supporting Commands

| Command      | Purpose                                                         |
| ------------ | --------------------------------------------------------------- |
| `/debug`     | Systematic error diagnosis, root-cause analysis                 |
| `/simplify`  | Reduce complexity without changing behavior                     |
| `/fix-issue` | Analyze and fix reported bugs (requires reproducing test first) |

---

## Core Principles

### Code Quality
- **Test-Driven Development** — failing test first, implementation second
- **Incremental slices** — Entity → Config → Repo → Handler → Validator → Controller → Test, always buildable at each step
- **Result over exceptions** — `Result<T>` for expected business failures; exceptions only for unrecoverable errors
- **Five-Axis Review** — Correctness, Readability, Architecture, Security, Performance

### Design Philosophy
- Progress over perfection — ship small, ship often
- Fix root causes, not symptoms
- The simplest design that could work
- Tests are proof, not afterthought
- Convention over configuration — follow existing patterns before inventing new ones

---

## Architecture

**Clean Architecture** with 5 layers, dependencies pointing inward (`API → Application → Domain`, `Infrastructure → Domain`):

```
┌──────────────────────────────────────────────────────────────────┐
│ Beacon.Api              Controllers, Middleware, DI wiring       │
│ Beacon.Application      MediatR handlers, DTOs, Validators,       │
│                         Mappers, feature folders                 │
│ Beacon.Domain           Entities, Enums, Repository interfaces    │
│                         (NO framework deps)                      │
│ Beacon.Infrashtructure  EF Core DbContext, Repositories, JWT      │
│                         service, SQL Server                      │
│ Beacon.Shared           Result<T>, ApiResponse<T>, Guards,        │
│                         Pagination, Constants                    │
└──────────────────────────────────────────────────────────────────┘
```

> ⚠️ **Known technical debt** — see the [Open Decisions](#open-decisions--technical-debt) section at the end of this document. The project name `Beacon.Infrashtructure` contains a typo that we have not yet corrected; it is tracked, not endorsed.

---

## Project Structure

```
Beacon-CA/
├── src/
│   ├── Beacon.Api/                          # Presentation layer
│   │   ├── Authorization/                   # [HasPermission], [AdminOnly], policy provider
│   │   ├── Controllers/
│   │   │   ├── Identity/
│   │   │   │   ├── AdminAuthController.cs   # POST api/v1/admin/auth/login|logout
│   │   │   │   ├── AuthController.cs        # POST api/v1/auth/register|login|logout|refresh-token
│   │   │   │   └── UsersController.cs       # PATCH api/v1/users/me, PUT api/v1/users/me/avatar
│   │   │   ├── Storage/
│   │   │   │   └── MediaController.cs       # CRUD api/v1/media
│   │   │   ├── BaseController.cs            # HandleResult<T>, CreatedResult<T>
│   │   │   └── DevicesController.cs         # POST api/v1/devices/register
│   │   ├── Extensions/
│   │   │   ├── AuthExtensions.cs
│   │   │   ├── HealthCheckExtensions.cs
│   │   │   └── SwaggerExtensions.cs
│   │   ├── HealthChecks/
│   │   │   └── MinioHealthCheck.cs
│   │   ├── Middleware/
│   │   │   └── ExceptionHandlingMiddleware.cs
│   │   ├── Backgroundjobs/                  # (scaffolding only)
│   │   ├── appsettings.json
│   │   └── Program.cs
│   │
│   ├── Beacon.Application/                  # Use-case layer (no framework deps except MediatR)
│   │   ├── Common/
│   │   │   ├── Behaviors/
│   │   │   │   └── ValidationBehavior.cs    # MediatR pipeline: auto-validate commands
│   │   │   ├── Exceptions/                  # NotFoundException, ConflictException, etc.
│   │   │   └── Interfaces/IService/         # ICurrentUserService, IJwtService, IStorageService, IImageProcessor
│   │   ├── DependencyInjection/
│   │   │   └── ApplicationServiceExtensions.cs
│   │   ├── Features/
│   │   │   ├── Identity/
│   │   │   │   ├── Commands/                # Register, Login, Logout, RefreshToken,
│   │   │   │   │                            #   LoginAdmin, LogoutAdmin, RegisterDevice,
│   │   │   │   │                            #   UpdateProfile, UpdateAvatar
│   │   │   │   ├── Queries/                 # GetCurrentUser, CheckEmailAvailability, CheckPhoneAvailability
│   │   │   │   ├── Dtos/                    # Request/Response DTOs (RegisterRequest, AuthResponse, UserProfileDto…)
│   │   │   │   ├── Services/
│   │   │   │   └── Validators/Identity/     # FluentValidation — one file per Command/Query
│   │   │   ├── Storage/
│   │   │   │   ├── Commands/                # Upload, SoftDelete, HardDelete
│   │   │   │   ├── Queries/                 # GetMediaById, ListMedia
│   │   │   │   ├── Dtos/                    # MediaDto, UploadMediaRequest
│   │   │   │   └── Validators/Storage/      # UploadMediaCommandValidator, ListMediaQueryValidator
│   │   │   ├── Checkins/                    # (scaffolding only)
│   │   │   ├── Group/                       # (scaffolding only)
│   │   │   ├── Messaging/                   # (scaffolding only)
│   │   │   ├── Notification/                # (scaffolding only)
│   │   │   └── Safety/                      # (scaffolding only)
│   │   ├── Mappings/
│   │   │   ├── Identity/                    # UserAuthMapper, UserProfileMapper, AdminAuthMapper
│   │   │   └── Storage/                     # MediaDtoMapper
│   │   └── Services/
│   │       └── CurrentUserService.cs
│   │
│   ├── Beacon.Domain/                       # Core domain — zero framework dependencies
│   │   ├── Common/
│   │   │   ├── BaseEntity.cs                # Guid Id
│   │   │   ├── AuditableEntity.cs           # + CreatedAtUtc, UpdatedAtUtc
│   │   │   └── SoftDeletableEntity.cs       # + IsDeleted (EF query filter)
│   │   ├── Entities/
│   │   │   ├── Identity/                    # User, Admin, Role, Permission, RefreshToken,
│   │   │   │                                #   RefreshTokenAdmin, AdminRole, RolePermission, UserDevice
│   │   │   ├── Storage/                     # MediaObject
│   │   │   ├── Checkins/                    # Checkin, CheckinMedia
│   │   │   ├── Safety/                      # AlertIncident, DailySafetyRecord, EmergencyContact
│   │   │   ├── Notification/                # NotificationDelivery
│   │   │   └── Settings/                    # SafetySetting, NotificationPreference, AppPreference
│   │   ├── Enums/                           # MediaType, MediaAccessType, StorageProvider,
│   │   │                                    #   DevicePlatform, NotificationChannel, SafetyStatus…
│   │   ├── IRepository/                     # IUserRepository, IAdminRepository,
│   │   │   └── Storage/                     #   IUserDeviceRepository, IMediaObjectRepository
│   │   └── Constants/
│   │
│   ├── Beacon.Infrashtructure/              # ⚠️ typo tracked — see Open Decisions
│   │   ├── Dependencyinjection/             # ⚠️ lowercase 'i' — tracked
│   │   │   └── InfrastructureServiceExtensions.cs
│   │   ├── Presistence/
│   │   │   ├── Configuration/               # IEntityTypeConfiguration<T> — one per entity
│   │   │   │   ├── Identity/
│   │   │   │   ├── Storage/
│   │   │   │   ├── Checkins/
│   │   │   │   ├── Safety/
│   │   │   │   ├── Notification/
│   │   │   │   └── Settings/
│   │   │   └── AppDbContext.cs
│   │   ├── Repository/
│   │   │   ├── Identity/                    # UserRepository, AdminRepository, UserDeviceRepository
│   │   │   └── Storage/                     # MediaObjectRepository
│   │   ├── Services/
│   │   │   ├── Storage/                     # MinioStorageService, ImageSharpProcessor, MinioBucketInitializer
│   │   │   └── JwtService.cs
│   │   └── Migrations/
│   │
│   └── Beacon.Shared/                       # Cross-cutting, no business logic
│       ├── Results/                         # Result<T>, Error, ErrorType
│       ├── Common/
│       │   ├── Responses/                   # ApiResponse<T>
│       │   └── Pagination/                  # PaginatedList<T>, CursorPagedResult<T>
│       └── Constants/
│           └── ErrorCodes.cs                # SCREAMING_SNAKE_CASE string constants
│
├── tests/
│   ├── Beacon.UnitTests/
│   │   ├── Identity/                        # LoginCommandHandlerTests, RegisterCommandHandlerTests…
│   │   └── Storage/                         # (to be populated)
│   └── Beacon.IntergrationTests/            # ⚠️ typo tracked — WebApplicationFactory tests
│
├── .claude/
│   └── CLAUDE.md                            # This file
├── Docs/
│   └── api-conventions.md
├── docker-compose.yml
└── Dockerfile
```

### Quy ước đặt file trong feature folder

Mỗi use case có thư mục riêng chứa cả Command/Query + Handler:

```
Features/Identity/Commands/Login/
    LoginCommand.cs          # IRequest<Result<AuthResponse>>
    LoginCommandHandler.cs   # IRequestHandler<LoginCommand, Result<AuthResponse>>
```

Validator đặt trong `Validators/{Module}/`:

```
Features/Identity/Validators/Identity/
    LoginRequestValidator.cs     # AbstractValidator<LoginCommand>
```

> Lý do tách: validator nằm ngoài use case folder để có thể tái dụng và dễ dò qua IDE.

---

## Mandatory Rules

All rules below are **non-negotiable**. Code review will reject PRs that violate them unless an explicit ADR documents the exception.

### Code Quality

| Rule              | Description                                                                |
| ----------------- | -------------------------------------------------------------------------- |
| `result-pattern`  | All service/handler methods return `Result` or `Result<T>`                 |
| `no-exceptions-for-flow` | Never throw for expected business failures                          |
| `null-safety`     | `GetById*` returns nullable; handlers convert to `Result.Failure.NotFound` |
| `naming`          | PascalCase classes/methods, camelCase locals, `_camelCase` private fields  |

### Architecture & Layering

| Rule              | Description                                                                          |
| ----------------- | ------------------------------------------------------------------------------------ |
| `layer-boundaries` | Domain has ZERO framework refs; Application depends only on Domain + Shared         |
| `no-direct-db`    | Handlers never touch `DbContext` — always go through a repository interface         |
| `handler-per-use-case` | One MediatR handler = one use case. No "AggregateService" god-classes          |
| `di-via-extensions` | No service registration in `Program.cs` — use the extension methods (see table)   |

### Data & Persistence

| Rule                       | Description                                                        |
| -------------------------- | ------------------------------------------------------------------ |
| `entity-base-class`        | Pick the right base: `BaseEntity`, `AuditableEntity`, or `SoftDeletableEntity` |
| `fluent-config-per-entity` | Every entity has an `IEntityTypeConfiguration<T>` class            |
| `soft-delete-in-dbcontext` | Query filters live in `OnModelCreating`, NOT in the config class   |
| `dbset-required`           | New entity = add `DbSet<T>` to `AppDbContext`                      |
| `no-n+1`                   | Use `Include` / projected DTOs; verify with integration tests      |

### API Conventions

| Rule              | Description                                                          |
| ----------------- | -------------------------------------------------------------------- |
| `api-envelope`    | Every response wrapped in `ApiResponse<T>` — see schema below        |
| `base-controller` | Inherit `BaseController`; use `HandleResult()` / `CreatedResult()`   |
| `route-kebab-case` | `/api/v1/users`, `/api/v1/alert-incidents` — never camelCase in URLs |
| `status-codes`    | 200 OK, 201 Created, 204 NoContent, 400/401/403/404/409/500 only     |

### Security (CRITICAL)

| Rule              | Description                                                         |
| ----------------- | ------------------------------------------------------------------- |
| `jwt-signing-key` | MUST come from secret store (User Secrets / Key Vault). Never in `appsettings.json` |
| `rbac`            | Use `[HasPermission("resource:action")]` or `[AdminOnly]`          |
| `input-validation` | FluentValidation on every inbound DTO                              |
| `no-pii-in-logs`  | Never log raw passwords, tokens, emails, phone numbers             |
| `soft-delete`     | Sensitive entities use `SoftDeletableEntity`, never hard delete     |

### Testing

| Rule              | Description                                                 |
| ----------------- | ----------------------------------------------------------- |
| `unit-per-handler` | Every handler has a corresponding unit test class          |
| `integration-per-endpoint` | Controllers covered by integration tests via `WebApplicationFactory` |
| `prove-it`        | Bug fix requires a failing test first (RED → GREEN)        |
| `coverage-floor`  | 70% line coverage on Application layer (non-blocking gate) |

---

## API Response Shape

All endpoints return `ApiResponse<T>` from `Beacon.Shared`:

```json
{
  "success": true,
  "message": "User retrieved",
  "code": null,
  "data": { "...": "..." },
  "errors": null
}
```

On failure:

```json
{
  "success": false,
  "message": "User not found",
  "code": "USER_NOT_FOUND",
  "data": null,
  "errors": ["User with id {guid} does not exist"]
}
```

---

## Result Pattern (Canonical Example)

```csharp
// Service / Handler
public async Task<Result<UserDto>> GetUserAsync(Guid id)
{
    var user = await _repo.GetByIdAsync(id);
    if (user is null)
        return Result.Failure<UserDto>(
            Error.NotFound(ErrorCodes.USER_NOT_FOUND, "User not found"));

    return Result.Success(_userProfileMapper.ToProfileDto(user));
}

// Controller
public async Task<IActionResult> GetUser(Guid id)
    => HandleResult(await _userService.GetUserAsync(id));

// For POST with 201 Created
public async Task<IActionResult> CreateUser(CreateUserDto dto)
    => CreatedResult("GetUser", await _userService.CreateAsync(dto));
```

---

## Entity Base Classes

| Base                    | Adds                                         | Use when                                       |
| ----------------------- | -------------------------------------------- | ---------------------------------------------- |
| `BaseEntity`            | `Guid Id`                                    | Lookup tables, reference data                  |
| `AuditableEntity`       | `CreatedAtUtc`, `UpdatedAtUtc`               | Any business entity you need to audit          |
| `SoftDeletableEntity`   | `IsDeleted` + EF query filter                | Users, orders, anything recoverable            |

---

## Domain Modules

Each module has mirrored folders in `Application/Features/{Module}/` and `Domain/Entities/{Module}/`.

| Module           | Status | Key Components                                                                 |
| ---------------- | ------ | ------------------------------------------------------------------------------ |
| **Identity**     | ✅ Done | `User`, JWT auth, refresh tokens, devices, Admin RBAC                          |
| **Safety**       | ✅ Done | Daily safety records, emergency contacts, alert incidents                      |
| **Checkins**     | ✅ Done | Checkin records with media attachments                                         |
| **Notification** | ✅ Done | Multi-channel delivery (email, SMS, push) with tracking                        |
| **Settings**     | ✅ Done | Per-user safety / notification / app preferences                               |
| **Storage**      | ✅ Done | Media objects, public/private access control                                   |
| **Group**        | 🚧 Scaffolding only                                                            |
| **Messaging**    | 🚧 Scaffolding only                                                            |

### Identity Sub-Structure

- User auth: Register / Login / Logout / RefreshToken — `Application/Features/Identity/Commands/`
- Admin auth: Login / Logout with RBAC (roles + permissions) — same folder
- Authorization attributes: `[HasPermission("x:y")]`, `[AdminOnly]` — `Api/Authorization/`

---

## EF Core Conventions

- **Configurations** live in `Infrashtructure/Presistence/Configuration/{Module}/` (Fluent API)
- **Soft-delete query filters** go in `AppDbContext.OnModelCreating`, NOT the config class
- New `IEntityTypeConfiguration<T>` classes are **auto-discovered** via `ApplyConfigurationsFromAssembly`
- Always register `DbSet<T>` in `AppDbContext` for new entities
- Enable sensitive data logging **only** in Development

---

## Exception Handling

Global middleware in `Beacon.Api` maps exceptions to HTTP status codes:

| Exception                | HTTP  |
| ------------------------ | ----- |
| `NotFoundException`      | 404   |
| `ConflictException`      | 409   |
| `UnauthorizedException`  | 401   |
| `ForbiddenException`     | 403   |
| `ValidationException`    | 400   |
| *unhandled*              | 500   |

> Throw these **only** for unexpected / unrecoverable errors. Expected business failures use `Result.Failure(...)`.

---

## Repository Interfaces

- Location: `src/Beacon.Domain/IRepository/`
- No generic `IRepository<T>` base — declare each interface explicitly with domain-meaningful methods
- Implementations: `src/Beacon.Infrashtructure/Repository/{Module}/`
- Repository methods should return `Entity?` (nullable) for single-item queries; handlers convert null → `Result.Failure.NotFound`

---

## Dependency Injection

`Program.cs` calls only extension methods. **Never register services directly in `Program.cs`.**

| Adding                       | Modify file                                                                 |
| ---------------------------- | --------------------------------------------------------------------------- |
| Repository                   | `Infrashtructure/Dependencyinjection/InfrastructureServiceExtensions.cs`    |
| Handler / Validator          | *Nothing* — auto-scanned by MediatR / FluentValidation                      |
| Mapper                       | `Application/DependencyInjection/ApplicationServiceExtensions.cs`           |
| Authorization policy         | `Api/Extensions/AuthExtensions.cs`                                          |
| Health check                 | `Api/Extensions/HealthCheckExtensions.cs`                                   |

### Program.cs pattern

```csharp
builder.Services.AddInfrastructure(builder.Configuration); // DbContext, Repos, JwtService
builder.Services.AddApplication();                         // MediatR, FluentValidation, Mappers
builder.Services.AddApiAuth(builder.Configuration);        // JWT Bearer, AuthZ, CORS
builder.Services.AddSwagger();
builder.Services.AddHealthChecking(builder.Configuration);
builder.Services.AddControllers();
```

> ⚠️ Infrastructure namespace uses lowercase `Dependencyinjection` (not `DependencyInjection`). Tracked in [Open Decisions](#open-decisions--technical-debt).

---

## Mapping Convention (Manual DTO Mapping)

**Current stance:** Manual mapping, no AutoMapper / Mapster. This is revisitable — see [Open Decisions](#open-decisions--technical-debt) for the evaluation of Mapperly.

### Pattern

- One mapper = one file = one `sealed class` per use case
- Location: `src/Beacon.Application/Mappings/{Module}/{Entity}{UseCase}Mapper.cs`
- Class name: `{Entity}{UseCase}Mapper` (e.g. `UserAuthMapper`, `UserProfileMapper`)
- Method name: `To{Dto}(entity, ...context)` (e.g. `ToAuthResponse`, `ToProfileDto`)
- DI lifetime: **Singleton** (stateless, pure)
- Inject into handlers via constructor

### Prohibited in mappers

- ❌ Business logic (validation, decision, computation) → move to Domain or handler
- ❌ I/O (DB, HTTP, file) — mapper is pure and sync
- ❌ `async` / `Task<TDto>` → signals a wrong design
- ❌ Mutable state → would break Singleton safety
- ❌ `static` class → we use instance classes to allow future DI dependencies
- ❌ Extension methods (`this Entity`) → use instance methods
- ❌ Generic `IMapper<TSource, TDest>` interface → over-engineering

### Example

```csharp
// src/Beacon.Application/Mappings/Identity/UserAuthMapper.cs
public sealed class UserAuthMapper
{
    public AuthResponse ToAuthResponse(
        User user, string accessToken, string refreshToken, DateTime expiresAt)
        => new()
        {
            UserId = user.Id,
            Username = user.Username,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAt = expiresAt
        };
}

// Handler
public class LoginCommandHandler(
    IUserRepository userRepo,
    IJwtService jwtService,
    UserAuthMapper authMapper)
    : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    // ...
    return Result<AuthResponse>.Success(
        authMapper.ToAuthResponse(user, accessToken, refreshToken, expiresAt));
}
```

### Nested objects → composition

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

### Lists → LINQ `Select`

```csharp
var dtos = users.Select(userListMapper.ToListItem).ToList();
```

Do NOT create `MapList()` wrappers — `Select` is idiomatic.

---

## Operations & Production Readiness

These concerns are **required before production launch**. Track each as its own ticket.

| Area                | Target State                                                                     | Status |
| ------------------- | -------------------------------------------------------------------------------- | ------ |
| **Observability**   | Serilog + OpenTelemetry (logs, traces, metrics) → OTLP exporter                  | ⏳ TODO |
| **Health Checks**   | `/health/live`, `/health/ready` (DB, Redis, external APIs)                       | ✅ Partial |
| **API Versioning**  | `Asp.Versioning.Http` with URL segment (`/api/v1/…`)                             | ⏳ TODO |
| **Rate Limiting**   | Built-in `AddRateLimiter` — per-IP for auth, per-user for API                    | ⏳ TODO |
| **Caching**         | `IDistributedCache` + Redis; cache keys follow `{module}:{entity}:{id}` pattern  | ⏳ TODO |
| **Background Jobs** | Hangfire or `Channels` — for notification delivery, alert escalation             | ⏳ TODO |
| **Secrets**         | User Secrets (dev) / Azure Key Vault / AWS Secrets Manager (prod)                | ⏳ TODO |
| **Idempotency**     | `Idempotency-Key` header on alert & notification endpoints (mobile retries!)     | ⏳ TODO |
| **Docker**          | Multi-stage Dockerfile + `docker-compose.yml` (API + SQL Server + Redis)         | ⏳ TODO |
| **CI/CD**           | GitHub Actions: restore → build → test → migration check → image push            | ⏳ TODO |

---

## Namespace Quirks (Read Before Coding)

| Folder / Project                          | Actual Namespace                       |
| ----------------------------------------- | -------------------------------------- |
| `src/Beacon.Infrashtructure` (project)    | `Beacon.Infrashtructure`               |
| `Infrashtructure/Dependencyinjection`     | `Beacon.Infrashtructure.Dependencyinjection` (lowercase `i`) |
| `Beacon.Domain/Entities/Settings/`        | `Beacon.Domain.Entities.Setting` (no `s`) |

> These quirks are **bugs we have not yet paid down**. See [Open Decisions](#open-decisions--technical-debt). New code following existing folders must match the existing namespace until we do the rename.

---

## Skills

Specialized workflows invoked via slash commands:

| Skill                  | Invoke with            | Purpose                                                        |
| ---------------------- | ---------------------- | -------------------------------------------------------------- |
| Create Entity          | `/create-entity`       | Domain entity + EF config only (no endpoint yet)               |
| Create Endpoint        | `/create-endpoint`     | Full vertical slice: Entity → Repo → Handler → Controller      |
| Add Validation         | `/add-validation`      | New FluentValidation validator for a DTO                       |
| Add Migration          | `/add-migration`       | After entity/EF config change, scaffold and review migration   |
| Write Unit Test        | `/write-unit-test`     | Generate unit-test skeleton for a new handler / service        |
| Setup DI               | `/setup-di`            | Register a service or create a new extension method            |
| Create Auth Handler    | `/create-auth-handler` | Reference the implemented User Auth pattern                    |
| Create Admin Auth      | `/create-admin-auth`   | Reference the implemented Admin Auth + RBAC pattern            |

---

## Agents

Invoke the right specialist for each task type.

### Development Agents

| Agent                    | Invoke when                                                      |
| ------------------------ | ---------------------------------------------------------------- |
| 🔧 **Backend Developer** | Handlers, services, repositories, EF config, background jobs     |
| 🏗️ **Systems Architect** | New module design, architectural decision, ADR authoring         |
| 📦 **Data Engineer**     | Complex EF queries, indexing, migration conflicts, N+1 diagnosis |

### Quality Agents

| Agent                     | Invoke when                                                        |
| ------------------------- | ------------------------------------------------------------------ |
| 👀 **Code Reviewer**      | Pre-merge PR review using the five-axis framework                  |
| 🧪 **Test Engineer**      | Test strategy, TDD coaching, coverage analysis, bug reproduction   |
| 🔒 **Security Auditor**   | JWT config review, RBAC verification, dependency vuln scan         |
| ✅ **QA Engineer**        | E2E test plan, integration test authoring, regression sweeps       |

### Research & Product

| Agent                | Invoke when                                                              |
| -------------------- | ------------------------------------------------------------------------ |
| 🔬 **Researcher**    | Compare NuGet packages, find .NET best practices, evaluate alternatives  |
| 🧭 **API Reviewer**  | Check a new endpoint for REST convention, security, naming, envelope     |
| 📋 **Project Manager** | User stories, sprint planning, status reporting                        |

---

## Reference Checklists

Quick-reference lists used during review and pre-deploy:

| Reference                    | Use For                                                |
| ---------------------------- | ------------------------------------------------------ |
| `security-checklist.md`      | Pre-deploy verification (JWT, secrets, CORS, HTTPS)    |
| `api-convention-checklist.md`| New endpoint review                                    |
| `testing-patterns.md`        | Arrange-Act-Assert structure, naming, anti-patterns    |
| `performance-checklist.md`   | N+1 detection, query plan review, response size        |
| `migration-checklist.md`     | Breaking-change detection before `dotnet ef update`    |

---

## Agent Behavior Guidelines

1. **Follow the workflow** — `/spec` → `/plan` → `/build` → `/test` → `/review` → `/deploy`
2. **Respect the Mandatory Rules** — violations require an ADR, not a shrug
3. **Test first** — failing test before implementation (RED → GREEN → REFACTOR)
4. **Incremental slices** — each commit must build; no "big bang" PRs
5. **Explain before acting** — describe the plan, get confirmation for non-trivial changes
6. **Fix root causes** — never patch a symptom to make a test pass
7. **Use the right specialist** — delegate to the agent that owns the domain
8. **Return `Result`, don't throw** — exceptions are for unrecoverable errors only
9. **Match existing patterns** — if a similar feature exists, mirror its structure
10. **Flag tech debt, don't hide it** — new "do not rename" rules require an ADR

---

## Open Decisions & Technical Debt

These are flagged for review rather than hidden as "permanent". Each should have an ADR before being closed out.

### 1. Project & namespace typos

- **Project name:** `Beacon.Infrashtructure` (should be `Infrastructure`)
- **Namespace:** `Dependencyinjection` (should be `DependencyInjection`)
- **Namespace:** `Entities.Setting` for folder `Entities/Settings/` (mismatched)

**Impact:** every new dev must remember to mis-spell; analyzers produce noise; search is harder.
**Recommendation:** schedule a rename sprint. A coordinated Visual Studio / Rider rename + single commit is ~30 min of work and immediately stops the bleeding. Delay compounds the cost.
**Status:** open. Not a blocker, but do not add more code that depends on the typo long-term.

### 2. Mapping library evaluation

Current stance is manual mapping. **Mapperly** (source-generator based, compile-time, zero reflection, type-safe, breaks the build on property mismatches) is materially different from AutoMapper/Mapster and deserves a formal evaluation before we have 100+ mapper classes.

**Action:** write an ADR comparing Mapperly vs. current manual approach on:
- Lines of code per mapper
- Refactor safety (does adding a property break the build?)
- Debuggability (can we step through the generated code?)
- Performance (benchmark against manual)

**Status:** open. Manual mapping remains the default until the ADR is published.

### 3. Mapper as `sealed class` Singleton vs. `static` methods

Current pattern inject mappers via DI "to allow future dependencies". In practice, the mapping rules prohibit any dependency-requiring logic. This is speculative generality.

**Alternatives to evaluate:**
- `static` methods (simpler, no DI, no allocation)
- Keep `sealed class` + Singleton but document when DI is actually needed

**Status:** open. Don't refactor existing mappers yet — decide in the ADR above.

### 4. MediatR licensing

MediatR v12 is the last free version. **v13+ is commercial** (confirmed by the author, 2024). We need an explicit decision:

| Option                                    | Trade-off                                                    |
| ----------------------------------------- | ------------------------------------------------------------ |
| Pin MediatR v12 forever                   | Free, but frozen — no security patches eventually            |
| Buy commercial license                    | Supported, but ongoing cost                                  |
| Migrate to **Mediator** (Martin Othamar)  | Source-gen, free, ~10× faster, small API differences          |
| Drop mediator pattern, use services       | Biggest refactor, simplest dependency tree                    |

**Status:** open. Must be decided before next major version bump.

### 5. Missing production concerns

See [Operations & Production Readiness](#operations--production-readiness) — every row marked ⏳ is an open debt.

---

## Change Log

This document is versioned alongside the code. When you make a structural change to the project (new module, new rule, new convention), update this file in the same PR and note the change here.

| Date       | Change                                          | Author  |
| ---------- | ----------------------------------------------- | ------- |
| 2026-04-19 | Initial restructure with workflow, rules, open decisions | Team    |
| 2026-04-20 | Add Project Structure section with full directory tree   | Team    |