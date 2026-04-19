---
name: plan
description: >
  Decompose một spec hoặc feature request thành vertical slices có thứ tự rõ ràng,
  theo đúng Beacon architecture: Test → Handler → Repo → EF Config → Controller.
---

# /plan — Planning & Task Breakdown (Beacon)

> "Vertical slices, not horizontal layers."
> Mỗi slice phải buildable và testable độc lập — TDD từ đầu.

## Mục đích

Chuyển một spec hoặc yêu cầu feature thành danh sách task có thứ tự,
mỗi task deliver end-to-end functionality theo đúng pipeline của Beacon.

## Điều kiện tiên quyết

- Spec đã được viết (qua `/spec`) hoặc requirements được mô tả rõ trong chat
- Đã đọc `CLAUDE.md` — hiểu mandatory rules và module status
- Biết module nào bị ảnh hưởng (xem Domain Modules table trong CLAUDE.md)

---

## Beacon Development Pipeline

```
/spec  →  /plan  →  /build  →  /test  →  /review  →  /deploy
Define    Slice    TDD       Verify    5-axis      Ship
```

**/plan** nằm giữa spec và build — output của plan là input cho `/build`.

---

## Mandatory Rules (phải enforce trong plan)

> Mọi slice **phải** tuân theo các rules sau. Nếu plan vi phạm → plan bị reject.

| Rule | Enforcement |
|------|------------|
| Handler PHẢI return `Result<T>` | Không trả raw DTO, không throw exception cho business failure |
| Business rules trong Domain | Entity methods hoặc Domain Service — KHÔNG trong Handler |
| `SaveChangesAsync` một lần / use case | Trong Repository — KHÔNG gọi nhiều lần trong Handler |
| Validator target Command | `AbstractValidator<{Verb}{Entity}Command>` — KHÔNG validate DTO |
| Mapper: 1 file = 1 use case | `sealed class`, Singleton, pure mapping — KHÔNG AutoMapper |
| Handler KHÔNG inject DbContext | Chỉ qua repository interface |
| Messages tiếng Việt | Tất cả validation messages và error messages |

---

## Phase 1: Analysis (Read-Only)

> ⚠️ **KHÔNG sửa code trong phase này.** Chỉ đọc.

### 1.1 Xác định scope

- Feature thuộc module nào?
- Module đang ở trạng thái gì? (Done / Scaffolding / New)
- Có cross-module dependency không? (VD: Notification cần Identity)

### 1.2 Survey codebase

| Tìm kiếm | Vị trí |
|----------|--------|
| Entity hiện có | `src/Beacon.Domain/Entities/{Module}/` |
| Repository interface | `src/Beacon.Domain/IRepository/` |
| Handler hiện có | `src/Beacon.Application/Features/{Module}/Commands\|Queries/` |
| EF Config | `src/Beacon.Infrashtructure/Persistence/Configuration/{Module}/` |
| Controller | `src/Beacon.Api/Controllers/` |
| Test hiện có | `tests/Beacon.UnitTests/{Module}/` |

### 1.3 Xác định business rules

Trước khi slice, liệt kê tất cả business rules của feature:

```markdown
## Business Rules cho {Feature}

- Rule 1: User không được tạo checkin nếu đang bị block
- Rule 2: Tối đa 10 checkin/ngày/user
- Rule 3: ...

→ Mỗi rule này phải nằm trong Domain Entity method hoặc Domain Service
→ KHÔNG implement business rule trong Handler (chỉ orchestrate)
```

---

## Phase 2: Vertical Slicing

### ✅ Đúng — Vertical Slice (TDD-first)

```
Slice 1: User có thể tạo Checkin mới
  → Test (RED) → Handler (mock) → Repo Interface → Repo Impl → EF Config → Controller

Slice 2: User có thể xem lịch sử Checkin
  → Test (RED) → Query Handler → Repo method → Controller
```

### ❌ Sai — Horizontal Layer (anti-pattern)

```
Task 1: Tạo tất cả Entity
Task 2: Tạo tất cả Repository
Task 3: Tạo tất cả Handler
```

---

## Phase 3: Beacon Vertical Slice Template (TDD Order)

> ⚠️ **Thứ tự TDD: Test (RED) trước, rồi mới implement.**

```markdown
## Slice {N}: [{Verb} {Entity}] — {mô tả use case}

**Module**: {Safety / Checkins / Identity / ...}
**Type**: Command (write) | Query (read-only)

---

### Bước 1 — Viết Test (RED) trước

File: `tests/Beacon.UnitTests/{Module}/{Verb}{Entity}HandlerTests.cs`

Viết test skeleton với mock repository — test SẼ FAIL vì handler chưa tồn tại:

- `Handle_ShouldReturnSuccess_When{HappyPath}` — verify `Result.IsSuccess = true` + `Result.Value`
- `Handle_ShouldReturnNotFound_When{Entity}DoesNotExist` — verify `ErrorType.NotFound` + `ErrorCode`
- `Handle_ShouldReturnFailure_When{BusinessRuleViolated}` — verify `ErrorType` + `ErrorCode`

→ Commit với test FAIL — đây là điểm khởi đầu TDD

---

### Bước 2 — Domain Entity / Business Rules

**Chỉ cần nếu có business rule mới.**

File: `src/Beacon.Domain/Entities/{Module}/{Entity}.cs`

```csharp
// Business rule trong Domain method — KHÔNG đặt trong Handler
public Result CanCreateCheckin(int dailyCount, int maxPerDay)
{
    if (IsBlocked)
        return Result.Failure(Error.Forbidden(ErrorCodes.USER_BLOCKED, "..."));
    if (dailyCount >= maxPerDay)
        return Result.Failure(Error.Validation(ErrorCodes.CHECKIN_LIMIT_EXCEEDED, "..."));
    return Result.Success();
}
```

**Rule:** Business rules phải ở Entity method HOẶC Domain Service — KHÔNG trong Handler.
Nếu bỏ qua → Anemic Domain Model.

---

### Bước 3 — Repository Interface

File: `src/Beacon.Domain/IRepository/I{Entity}Repository.cs`

Method mới nếu cần:
```csharp
Task<{Entity}?> GetByIdAsync(Guid id, CancellationToken ct);
Task AddAsync({Entity} entity, CancellationToken ct);
// SaveChangesAsync nằm TRONG repository implementation — KHÔNG expose ra interface
```

> **Transaction rule:** `SaveChangesAsync` được gọi **một lần** trong Repository.
> Handler KHÔNG gọi SaveChanges trực tiếp. Không có partial save giữa chừng.

---

### Bước 4 — Command / Query + Handler

**Command:**
`src/Beacon.Application/Features/{Module}/Commands/{Verb}{Entity}Command.cs`

```csharp
public record {Verb}{Entity}Command({Verb}{Entity}Request Request)
    : IRequest<Result<{Entity}Dto>>;
```

**Handler:**
`src/Beacon.Application/Features/{Module}/Commands/{Verb}{Entity}Handler.cs`

```csharp
// ✅ Đúng — Handler chỉ orchestrate, không chứa business logic
public async Task<Result<{Entity}Dto>> Handle(
    {Verb}{Entity}Command command, CancellationToken ct)
{
    // 1. Fetch — null → NotFound
    var entity = await _repo.GetByIdAsync(command.Id, ct);
    if (entity is null)
        return Result.Failure<{Entity}Dto>(
            Error.NotFound(ErrorCodes.{ENTITY}_NOT_FOUND, "..."));

    // 2. Domain validation — business rule trong entity method
    var canProceed = entity.{BusinessMethod}(...);
    if (canProceed.IsFailure)
        return Result.Failure<{Entity}Dto>(canProceed.Error);

    // 3. Mutate + persist
    entity.{UpdateMethod}(...);
    await _repo.UpdateAsync(entity, ct);  // SaveChangesAsync inside repo

    // 4. Map + return
    return Result.Success(_mapper.To{Entity}Dto(entity));
}
```

**Bắt buộc:**
- ✅ Return `Result<T>` — không trả raw DTO
- ✅ Business error → `Result.Failure(Error.{Type}(ErrorCodes.X, "msg"))` — không throw
- ✅ KHÔNG inject `AppDbContext` — chỉ dùng `I{Entity}Repository`
- ✅ `CancellationToken` được pass xuống tất cả async call

---

### Bước 5 — Validator

File: `src/Beacon.Application/Features/{Module}/Validators/{Verb}{Entity}CommandValidator.cs`

```csharp
// PHẢI extend AbstractValidator<Command> — không phải DTO
public class {Verb}{Entity}CommandValidator : AbstractValidator<{Verb}{Entity}Command>
{
    public {Verb}{Entity}CommandValidator()
    {
        RuleFor(x => x.Request.{Field})
            .NotEmpty().WithMessage("{Field} không được để trống.")  // tiếng Việt
            .MaximumLength(100).WithMessage("{Field} không được vượt quá 100 ký tự.");
    }
}
```

---

### Bước 6 — DTO + Mapper

**Response DTO:** `src/Beacon.Application/Features/{Module}/DTOs/{Entity}Dto.cs`
**Request DTO:** `src/Beacon.Application/Features/{Module}/DTOs/{Verb}{Entity}Request.cs`

**Mapper:**
`src/Beacon.Application/Mappings/{Module}/{Entity}{UseCase}Mapper.cs`

```csharp
// 1 mapper = 1 use case — pure property assignment, không có logic
public sealed class {Entity}{UseCase}Mapper
{
    public {Entity}Dto To{Entity}Dto({Entity} entity) => new()
    {
        Id = entity.Id,
        // ...
    };
}
```

**Đăng ký Singleton:** `ApplicationServiceExtensions.cs`
> ❌ Không dùng AutoMapper / Mapster — CLAUDE.md mandatory rule.
> ❌ Không dùng `static` class — phải instance để DI được sau này.
> ✅ 1 mapper class = 1 use case — không phình to.

---

### Bước 7 — Repository Implementation + EF Config

**Chỉ cần nếu entity mới hoặc method mới.**

**EF Config:**
`src/Beacon.Infrashtructure/Persistence/Configuration/{Module}/{Entity}Configuration.cs`

```csharp
public class {Entity}Configuration : IEntityTypeConfiguration<{Entity}>
{
    public void Configure(EntityTypeBuilder<{Entity}> builder)
    {
        builder.ToTable("{Entity}s");
        // Soft delete query filter → KHÔNG đặt ở đây
        // → Đặt trong AppDbContext.OnModelCreating
    }
}
```

- Thêm `DbSet<{Entity}>` vào `AppDbContext`
- Soft delete filter → `AppDbContext.OnModelCreating` (không trong config)

**Repository Implementation:**
`src/Beacon.Infrashtructure/Repository/{Module}/{Entity}Repository.cs`

```csharp
public async Task AddAsync({Entity} entity, CancellationToken ct)
{
    await _context.{Entities}.AddAsync(entity, ct);
    await _context.SaveChangesAsync(ct);  // SaveChanges một lần ở đây
}
```

Đăng ký DI: `InfrastructureServiceExtensions.cs`

---

### Bước 8 — EF Migration (nếu có schema change)

```bash
dotnet ef migrations add {MigrationName} \
  --project src/Beacon.Infrashtructure \
  --startup-project src/Beacon.Api
```

Review migration file trước khi apply — confirm không có breaking change.

---

### Bước 9 — Controller Endpoint

File: `src/Beacon.Api/Controllers/{Module}Controller.cs`

```csharp
[Authorize]  // hoặc [HasPermission("x:y")] hoặc [AllowAnonymous]
[HttpPost]
[Route("api/v1/{resource-plural}")]  // lowercase kebab-case, route cố định
public async Task<IActionResult> {ActionName}(
    [{FromBody}] {Verb}{Entity}Request request,
    CancellationToken ct)
{
    var command = new {Verb}{Entity}Command(request);
    // POST tạo mới → CreatedResult | GET/PUT/DELETE → HandleResult
    return CreatedResult("{GetActionName}", await _mediator.Send(command, ct));
}
```

---

### Bước 10 — Hoàn thiện Test (GREEN + Integration)

**Unit Test (GREEN):** Chạy lại test từ Bước 1 — phải pass.

**Integration Test:**
`tests/Beacon.IntergrationTests/{Module}/{Module}ControllerTests.cs`

Bắt buộc cover đủ:
- [ ] `200 OK` — happy path, verify `ApiResponse<T>.Success = true` + `Data` đầy đủ
- [ ] `400 Bad Request` — validation error, verify `Errors` không empty (tiếng Việt)
- [ ] `401 Unauthorized` — không có token, verify `ApiResponse.Success = false`
- [ ] `403 Forbidden` — có token nhưng thiếu permission, verify `ApiResponse.Success = false`
- [ ] `404 Not Found` — entity không tồn tại (nếu applicable)

---

**Acceptance Criteria (mỗi slice):**
- [ ] `dotnet build` — 0 error, 0 warning
- [ ] Unit test: GREEN — success + failure cases với `ErrorType` + `ErrorCode`
- [ ] Integration test: 200 / 400 / 401 / 403 pass
- [ ] Swagger: endpoint hiện đúng route, auth requirement
- [ ] Response luôn là `ApiResponse<T>` — không có raw object

**Dependencies**: [Slice IDs phải complete trước]
```

---

## Phase 4: Ordering Rules

1. **Test trước (TDD)** — Viết test skeleton TRƯỚC khi có implementation
2. **Foundation** — Entity + EF Config + Migration nếu schema mới
3. **Risk-first** — Slice phức tạp / uncertain làm sớm
4. **Domain rules trước persistence** — Business rule trong Entity method trước khi lo DB
5. **Auth/RBAC cuối** — Thêm `[HasPermission]` sau khi business logic đã đúng
6. **Migration riêng** — 1 task EF migration tách biệt với business slice

---

## Phase 5: Checkpoints

```markdown
---
## ✅ Checkpoint: {Tên giai đoạn} Complete

**Verify trước khi tiếp tục**:
- [ ] `dotnet build` — 0 error, 0 warning
- [ ] `dotnet test tests/Beacon.UnitTests` — tất cả GREEN
- [ ] `dotnet test tests/Beacon.IntergrationTests` — tất cả GREEN
- [ ] Integration test cover đủ: 200 / 400 / 401 / 403
- [ ] `code-reviewer`: review slice vừa xong (nếu complex)
- [ ] `security-auditor`: kiểm tra auth coverage (nếu có endpoint mới)

---
```

---

## Output Format

```markdown
# Plan: [{Feature Name}]

**Module**: {Module}
**Phạm vi**: {N} slices

---

## Phase 1: Foundation

### Slice 1.1: Domain Entity + Business Rules
[Beacon Slice Template — từ Bước 2]
**Dependencies**: Không có

### Slice 1.2: EF Migration
[Migration Task]
**Dependencies**: Slice 1.1

---
## ✅ Checkpoint: Foundation Complete

---

## Phase 2: Core Use Cases (TDD)

### Slice 2.1: {Command Handler}
[Beacon Slice Template — Bước 1→10]
**Dependencies**: Slice 1.1, 1.2

### Slice 2.2: {Query Handler}
[Beacon Slice Template]
**Dependencies**: Slice 1.1

---
## ✅ Checkpoint: Core Complete

---

## Phase 3: Auth & Polish

### Slice 3.1: RBAC — thêm [HasPermission] cho endpoints
### Slice 3.2: Edge case + validation tighten

---
## ✅ Final Checkpoint

- [ ] `code-reviewer`: review toàn bộ module
- [ ] `security-auditor`: audit auth + sensitive data
- [ ] `test-engineer`: verify coverage ≥ 70% Application layer
- [ ] Ready for `/deploy`
```

---

## Sau khi plan xong

1. **Present plan** — giải thích thứ tự và lý do từng phase
2. **Chờ approval** — không build trước khi được confirm
3. **Chạy `/build`** — implement từng slice theo thứ tự TDD
4. **Checkpoint** — gọi `code-reviewer` sau mỗi phase lớn
