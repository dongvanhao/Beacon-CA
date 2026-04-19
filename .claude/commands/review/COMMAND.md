---
name: review
description: >
  Five-axis code review cho Beacon — Correctness, Readability, Architecture,
  Security, Performance. Gọi sau khi /build hoàn thành, trước khi merge.
---

# /review — Five-Axis Code Review (Beacon)

> "Approve khi code cải thiện rõ ràng so với trước — dù chưa hoàn hảo."
> Progress over perfection. Mỗi review phải để codebase tốt hơn trước.

## Mục đích

Review code trước khi merge, bắt các vi phạm mandatory rules, và đưa
feedback actionable theo five-axis framework của Beacon.

## Cách dùng

```
/review AuthController mới tại src/Beacon.Api/Controllers/AuthController.cs
/review toàn bộ Identity module sau khi thêm RBAC
/review CreateCheckinHandler + Validator + Controller
/review PR — tất cả changes trong branch hiện tại
```

---

## Workflow

### Bước 1: Đọc Test Trước

> Tests reveal intent — đọc test trước khi đọc implementation.

```bash
# Tìm test liên quan
tests/Beacon.UnitTests/{Module}/{Verb}{Entity}HandlerTests.cs
tests/Beacon.IntergrationTests/{Module}/{Module}ControllerTests.cs
```

- Test có mô tả đúng behavior không?
- Test có cover failure path không? (không chỉ happy path)
- Validator test có dùng `Theory + InlineData` không?

### Bước 2: Review Theo Five-Axis

Chạy qua từng axis theo thứ tự bên dưới.

### Bước 3: Output Theo Format Chuẩn

---

## Five-Axis Review Framework

### Axis 1 — Correctness (Tính đúng đắn)

**Result Pattern:**
- [ ] Handler return `Result<T>` — không trả raw DTO, không throw exception cho business failure
- [ ] `null` từ repository → `Result.Failure(Error.NotFound(ErrorCodes.X, "msg tiếng Việt"))`
- [ ] Business error dùng `ErrorType` đúng: `NotFound / Conflict / Validation / Unauthorized / Forbidden`
- [ ] `ErrorCode` dùng constant từ `ErrorCodes` — không hardcode string

**Edge Cases:**
- [ ] Duplicate entity → `Result.Failure(Error.Conflict(...))`
- [ ] Expired / invalid token → `Result.Failure(Error.Unauthorized(...))`
- [ ] Empty list → trả empty `Result.Success([])` không phải 404

**Async:**
- [ ] `CancellationToken` được pass xuống **tất cả** async call (kể cả repository)
- [ ] Không có `.Result` / `.Wait()` — toàn bộ async/await đúng

**Validator:**
- [ ] Validator extend `AbstractValidator<{Verb}{Entity}Command>` — KHÔNG validate DTO trực tiếp
- [ ] `ValidationBehavior` pipeline sẽ intercept — verify validator được register

**Data Consistency:**
- [ ] `SaveChangesAsync` được gọi **một lần** trong Repository — không gọi trong Handler
- [ ] Không có partial save (add rồi fail rồi không rollback)

---

### Axis 2 — Readability & Simplicity (Dễ đọc)

**Naming:**
- [ ] Handler: `{Verb}{Entity}Handler` — VD: `CreateCheckinHandler`
- [ ] Command: `{Verb}{Entity}Command` — VD: `CreateCheckinCommand`
- [ ] Mapper: `{Entity}{UseCase}Mapper` — VD: `CheckinDetailMapper`
- [ ] Validator: `{Verb}{Entity}CommandValidator`

**Complexity:**
- [ ] Handler ≤ 70 dòng — nếu dài hơn, xem xét tách Domain method
- [ ] Không có deep nesting (if trong if trong if) — dùng early return
- [ ] Không có magic number / string — dùng `ErrorCodes.X` hoặc constant

**Business Logic Placement:**
- [ ] Business rules trong Domain Entity method hoặc Domain Service
- [ ] Handler chỉ orchestrate: Fetch → Validate (domain) → Mutate → Persist → Map → Return
- [ ] Mapper không chứa business logic — chỉ property assignment thuần

**File Location:**
- [ ] Handler: `Application/Features/{Module}/Commands\|Queries/`
- [ ] Validator: `Application/Features/{Module}/Validators/`
- [ ] Mapper: `Application/Mappings/{Module}/`
- [ ] Entity: `Domain/Entities/{Module}/`
- [ ] Repo Interface: `Domain/IRepository/`

---

### Axis 3 — Architecture (Kiến trúc)

**Layer Boundaries (STRICT):**
- [ ] `Beacon.Domain` — ZERO framework dependency (không EF Core, không MediatR)
- [ ] `Beacon.Application` — chỉ depend on Domain + Shared, KHÔNG depend on Infrastructure
- [ ] Handler KHÔNG inject `AppDbContext` — chỉ dùng repository interface
- [ ] Controller KHÔNG chứa business logic — chỉ `_mediator.Send(command, ct)`

**CQRS:**
- [ ] 1 handler = 1 use case — không có "AggregateHandler"
- [ ] Command = write operation; Query = read-only
- [ ] Không reuse handler cho nhiều use case khác nhau

**DI Registration:**
- [ ] Repository mới → `InfrastructureServiceExtensions.cs`
- [ ] Mapper mới → `ApplicationServiceExtensions.cs` (Singleton)
- [ ] KHÔNG đăng ký service trực tiếp trong `Program.cs`

**EF Core:**
- [ ] Entity mới có `IEntityTypeConfiguration<T>` riêng
- [ ] Entity mới có `DbSet<T>` trong `AppDbContext`
- [ ] Soft delete query filter trong `AppDbContext.OnModelCreating` — KHÔNG trong config class

**Mapping:**
- [ ] `sealed class`, Singleton, instance method `To{Dto}(entity, ...context)`
- [ ] Không dùng AutoMapper / Mapster / generic `IMapper<T>`
- [ ] Lists → `Select(mapper.ToDto).ToList()` — không có `MapList()` wrapper

---

### Axis 4 — Security (Bảo mật)

**Authorization:**
- [ ] Mọi endpoint có một trong: `[Authorize]`, `[HasPermission("x:y")]`, `[AdminOnly]`, `[AllowAnonymous]`
- [ ] Endpoint không có attribute nào = **security gap**
- [ ] `[AllowAnonymous]` chỉ trên auth endpoint thực sự public (Register, Login, RefreshToken)
- [ ] `[HasPermission]` dùng đúng permission string từ seeded permissions

**Validation:**
- [ ] Mọi Command/Query có validator
- [ ] `FluentValidation` được chạy qua pipeline — không bypass

**Sensitive Data:**
- [ ] Password không trong log, response, hay plain-text storage
- [ ] Token không trong application log
- [ ] Error response không expose stack trace (chỉ Development)

**JWT / Secrets:**
- [ ] JWT key KHÔNG trong `appsettings.json`
- [ ] Connection string không chứa password trong source

---

### Axis 5 — Performance (Hiệu năng)

**N+1 Queries:**
- [ ] List query dùng `Include()` hoặc projected DTO — không lazy load trong loop
- [ ] Verify bằng integration test hoặc SQL log

**Pagination:**
- [ ] List endpoint có `PageNumber` + `PageSize` — không return unbounded list

**Read Optimization:**
- [ ] Read-only query dùng `AsNoTracking()`
- [ ] Select chỉ cột cần thiết — không `SELECT *` ẩn

**Async:**
- [ ] Tất cả I/O là async — không block thread

---

## Beacon-Specific Checklist (Quick Reference)

| # | Rule | Severity nếu vi phạm |
|---|------|---------------------|
| 1 | Controller kế thừa `BaseController` | 🔴 Critical |
| 2 | POST → `CreatedResult()` / Others → `HandleResult()` | 🟡 Warning |
| 3 | Route: `/api/v1/{resource-plural}` kebab-case cố định | 🟡 Warning |
| 4 | Response = `ApiResponse<T>` — không trả raw object | 🔴 Critical |
| 5 | Handler return `Result<T>` — không throw business exception | 🔴 Critical |
| 6 | Validator target Command — không target DTO | 🔴 Critical |
| 7 | Mapper: `sealed class` + Singleton + pure mapping | 🟡 Warning |
| 8 | No AutoMapper / Mapster | 🔴 Critical |
| 9 | Handler không inject DbContext | 🔴 Critical |
| 10 | `SaveChangesAsync` một lần trong Repository | 🔴 Critical |
| 11 | `ErrorCodes` thống nhất — không hardcode string | 🟡 Warning |
| 12 | Messages validation bằng tiếng Việt | 🟢 Suggestion |
| 13 | `CancellationToken` pass xuống tất cả async | 🟡 Warning |
| 14 | Mọi endpoint có auth attribute | 🔴 Critical |
| 15 | Business rule trong Domain — không trong Handler | 🟡 Warning |

---

## Delegate Khi Cần

| Scope phức tạp | Agent |
|----------------|-------|
| JWT config, RBAC, secrets | `security-auditor` |
| Test coverage, TDD quality | `test-engineer` |
| Architecture decision | `systems-architect` |
| Package comparison | `researcher` |

---

## Output Format

```markdown
## Review Summary

**Scope**: [file / module / PR]
**Overall**: APPROVE | REQUEST CHANGES | NEEDS DISCUSSION

---

### 🔴 Critical (phải sửa trước merge)

`[File.cs:LINE]` Mô tả vấn đề
→ Gợi ý sửa cụ thể (code snippet nếu cần)

### 🟡 Important (nên sửa, có thể block)

`[File.cs:LINE]` Mô tả vấn đề
→ Gợi ý sửa

### 🟢 Suggestions (tùy chọn)

`[File.cs:LINE]` Nit: mô tả
→ Gợi ý

### ✅ Positives (điểm tốt)

- ...

---

**Điểm**: X/10

| Axis | Score | Lý do chính |
|------|-------|------------|
| Correctness | X/10 | ... |
| Readability | X/10 | ... |
| Architecture | X/10 | ... |
| Security | X/10 | ... |
| Performance | X/10 | ... |

**Ưu tiên fix**: (1) vấn đề quan trọng nhất, (2) thứ hai
```

---

## Severity Labels

| Label | Ý nghĩa |
|-------|---------|
| 🔴 **Critical** | Merge blocker — vi phạm mandatory rule hoặc security |
| 🟡 **Important** | Nên sửa — có thể block nếu không có lý do rõ ràng |
| 🟢 **Suggestion** | Cải thiện tốt — tùy chọn |
| `Nit:` | Style nhỏ — hoàn toàn optional |
| `FYI:` | Thông tin — không cần hành động |
| ✅ | Điểm tốt — cần highlight để reinforce pattern |

---

## Bước Tiếp Theo

- **APPROVE** → Merge, chạy `/deploy` checklist
- **REQUEST CHANGES** → Fix issues, re-run `/review`
- **NEEDS DISCUSSION** → Tag `systems-architect` hoặc team lead
