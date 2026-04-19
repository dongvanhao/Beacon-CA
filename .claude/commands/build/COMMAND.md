---
name: build
description: >
  Implement tasks incrementally theo TDD và vertical slices của Beacon.
  Mỗi increment phải buildable và testable — RED → GREEN → REFACTOR.
---

# /build — Incremental Implementation (Beacon)

> "The simplest thing that could work."
> Implement từng task một — không vượt quá một slice trước khi test pass.

## Mục đích

Implement các task từ `/plan` theo đúng thứ tự TDD.
Mỗi increment để lại codebase ở trạng thái **buildable, tested, deployable**.

## Điều kiện tiên quyết

- Plan đã được approve (từ `/plan`)
- Biết acceptance criteria của từng task
- Moq + FluentAssertions đã được `dotnet add` vào test projects

---

## Beacon Build Pipeline

```
/plan (approved)
  ↓
/build — cho từng slice:
  ├── Step 1: Load Context
  ├── Step 2: RED   — Viết failing test
  ├── Step 3: GREEN — Implement tối thiểu
  ├── Step 4: REFACTOR — Clean up
  └── Step 5: Verify & Commit
  ↓
Checkpoint pass → Slice tiếp theo
  ↓
/review (sau khi tất cả slice done)
```

---

## Workflow: Cho Mỗi Task

### Step 1: Load Context

```
1. Đọc acceptance criteria của task trong plan
2. Tìm pattern tương tự trong codebase:
   - Handler tương tự: Application/Features/{Module}/Commands/
   - Controller tương tự: Api/Controllers/
   - Test tương tự: tests/Beacon.UnitTests/{Module}/
3. Hiểu interfaces đang có:
   - Repository interface: Domain/IRepository/
   - Result<T>, ErrorCodes, ErrorType: Beacon.Shared
```

**Không code gì ở bước này.** Chỉ đọc và hiểu context.

---

### Step 2: RED — Viết Failing Test

> **Viết test trước khi có implementation.** Test phải FAIL.

#### Handler Unit Test (RED)

```csharp
// tests/Beacon.UnitTests/{Module}/{Verb}{Entity}HandlerTests.cs

public class CreateCheckinHandlerTests
{
    private readonly Mock<ICheckinRepository> _repoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly CheckinMapper _mapper = new();
    private readonly CreateCheckinHandler _sut;

    public CreateCheckinHandlerTests()
    {
        _sut = new CreateCheckinHandler(
            _repoMock.Object,
            _userRepoMock.Object,
            _mapper);
    }

    // ← Test này PHẢI FAIL vì handler chưa tồn tại
    [Fact]
    public async Task Handle_ShouldReturnCheckinDto_WhenRequestIsValid()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), IsBlocked = false };
        var command = new CreateCheckinCommand(new CreateCheckinRequest
        {
            UserId = user.Id,
            Latitude = 10.762622,
            Longitude = 106.660172,
            Note = "Test checkin"
        });

        _userRepoMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _repoMock
            .Setup(r => r.AddAsync(It.IsAny<Checkin>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert — verify Result<T> đầy đủ
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenUserDoesNotExist()
    {
        _userRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new CreateCheckinCommand(new CreateCheckinRequest
        {
            UserId = Guid.NewGuid()
        });

        var result = await _sut.Handle(command, CancellationToken.None);

        // Verify 3 chiều: IsSuccess + ErrorType + ErrorCode
        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be(ErrorCodes.USER_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserIsBlocked()
    {
        var user = new User { Id = Guid.NewGuid(), IsBlocked = true };

        _userRepoMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new CreateCheckinCommand(new CreateCheckinRequest
        {
            UserId = user.Id
        });

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be(ErrorCodes.USER_BLOCKED);
    }
}
```

**Chạy test — xác nhận FAIL:**

```bash
dotnet test tests/Beacon.UnitTests --filter "CreateCheckinHandlerTests"
# Expected: FAIL (handler chưa tồn tại)
```

---

### Step 3: GREEN — Minimal Implementation

> Viết code **tối thiểu** để test pass. Không thêm gì ngoài acceptance criteria.

#### Thứ tự implement (theo Beacon slice):

**3a. Domain Entity / Business Rule (nếu cần)**

```csharp
// src/Beacon.Domain/Entities/Checkins/Checkin.cs

public class Checkin : AuditableEntity
{
    public Guid UserId { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public string? Note { get; private set; }

    // Business rule nằm trong Domain — KHÔNG trong Handler
    public static Result<Checkin> Create(User user, double lat, double lng, string? note)
    {
        if (user.IsBlocked)
            return Result.Failure<Checkin>(
                Error.Forbidden(ErrorCodes.USER_BLOCKED, "Tài khoản đã bị khóa."));

        return Result.Success(new Checkin
        {
            UserId = user.Id,
            Latitude = lat,
            Longitude = lng,
            Note = note
        });
    }
}
```

**3b. Repository Interface (nếu method mới)**

```csharp
// src/Beacon.Domain/IRepository/ICheckinRepository.cs
public interface ICheckinRepository
{
    Task AddAsync(Checkin checkin, CancellationToken ct);
    Task<Checkin?> GetByIdAsync(Guid id, CancellationToken ct);
}
```

**3c. Handler**

```csharp
// src/Beacon.Application/Features/Checkins/Commands/CreateCheckinHandler.cs

public class CreateCheckinHandler(
    ICheckinRepository checkinRepo,
    IUserRepository userRepo,
    CheckinMapper mapper)
    : IRequestHandler<CreateCheckinCommand, Result<CheckinDto>>
{
    public async Task<Result<CheckinDto>> Handle(
        CreateCheckinCommand command, CancellationToken ct)
    {
        // 1. Fetch — null → NotFound
        var user = await userRepo.GetByIdAsync(command.Request.UserId, ct);
        if (user is null)
            return Result.Failure<CheckinDto>(
                Error.NotFound(ErrorCodes.USER_NOT_FOUND, "Người dùng không tồn tại."));

        // 2. Domain factory — business rule trong Entity
        var checkinResult = Checkin.Create(
            user, command.Request.Latitude, command.Request.Longitude, command.Request.Note);

        if (checkinResult.IsFailure)
            return Result.Failure<CheckinDto>(checkinResult.Error);

        // 3. Persist — SaveChangesAsync nằm trong Repository
        await checkinRepo.AddAsync(checkinResult.Value, ct);

        // 4. Map + return
        return Result.Success(mapper.ToCheckinDto(checkinResult.Value));
    }
}
```

**Chạy test — xác nhận GREEN:**

```bash
dotnet test tests/Beacon.UnitTests --filter "CreateCheckinHandlerTests"
dotnet build  # phải pass 0 error
```

---

### Step 4: REFACTOR — Improve Quality

> Clean up **trong khi test vẫn xanh**. Không thêm feature mới.

Checklist refactor:

- [ ] Naming rõ ràng — không có `temp`, `data`, `obj`
- [ ] Không có magic string — dùng `ErrorCodes.X` thay vì hardcode
- [ ] Handler ≤ 70 dòng — nếu dài hơn, tách Domain method
- [ ] Không có comment giải thích code hiển nhiên — chỉ giữ comment explain "why"
- [ ] `CancellationToken` được pass xuống tất cả async call

```bash
# Verify test vẫn xanh sau refactor
dotnet test tests/Beacon.UnitTests --filter "CreateCheckinHandlerTests"
```

---

### Step 5: Verify & Commit

**Verify full suite trước khi commit:**

```bash
# Build toàn bộ solution
dotnet build src/Beacon.sln

# Unit tests
dotnet test tests/Beacon.UnitTests

# Integration tests (nếu có changes ảnh hưởng API)
dotnet test tests/Beacon.IntergrationTests
```

**Commit message convention:**

```bash
git add .
git commit -m "feat(checkins): add CreateCheckinCommand + handler + domain rule"

# Format:
# feat({module}): {mô tả ngắn}
# fix({module}): {mô tả bug fix}
# test({module}): {mô tả test thêm}
# refactor({module}): {mô tả refactor}
# chore: {migration, DI registration, config}
```

**Cập nhật plan:**

```markdown
# Trong plan (Docs/ hoặc chat)
- [x] Slice 2.1: CreateCheckinCommand + Handler ← đánh dấu xong
- [ ] Slice 2.2: GetCheckinListQuery
```

---

## Validator Implementation

```csharp
// src/Beacon.Application/Features/Checkins/Validators/CreateCheckinCommandValidator.cs

public class CreateCheckinCommandValidator : AbstractValidator<CreateCheckinCommand>
{
    public CreateCheckinCommandValidator()
    {
        RuleFor(x => x.Request.UserId)
            .NotEmpty().WithMessage("UserId không được để trống.");

        RuleFor(x => x.Request.Latitude)
            .InclusiveBetween(-90, 90)
            .WithMessage("Vĩ độ phải nằm trong khoảng -90 đến 90.");

        RuleFor(x => x.Request.Longitude)
            .InclusiveBetween(-180, 180)
            .WithMessage("Kinh độ phải nằm trong khoảng -180 đến 180.");

        RuleFor(x => x.Request.Note)
            .MaximumLength(500)
            .WithMessage("Ghi chú không được vượt quá 500 ký tự.");
    }
}
```

---

## Controller Implementation

```csharp
// src/Beacon.Api/Controllers/CheckinsController.cs

[ApiController]
[Route("api/v1/checkins")]  // route cố định — không dùng [controller]
[Authorize]
public class CheckinsController(IMediator mediator) : BaseController
{
    [HttpPost]
    public async Task<IActionResult> CreateCheckin(
        [FromBody] CreateCheckinRequest request,
        CancellationToken ct)
        => CreatedResult("GetCheckin",
            await mediator.Send(new CreateCheckinCommand(request), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCheckin(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(new GetCheckinQuery(id), ct));
}
```

---

## Repository Implementation

```csharp
// src/Beacon.Infrashtructure/Repository/Checkins/CheckinRepository.cs

public class CheckinRepository(AppDbContext context) : ICheckinRepository
{
    public async Task AddAsync(Checkin checkin, CancellationToken ct)
    {
        await context.Checkins.AddAsync(checkin, ct);
        await context.SaveChangesAsync(ct);  // SaveChanges một lần ở đây
    }

    public async Task<Checkin?> GetByIdAsync(Guid id, CancellationToken ct)
        => await context.Checkins
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
}
```

Đăng ký: `InfrastructureServiceExtensions.cs`:

```csharp
services.AddScoped<ICheckinRepository, CheckinRepository>();
```

---

## EF Migration (khi có schema change)

```bash
dotnet ef migrations add AddCheckinTable \
  --project src/Beacon.Infrashtructure \
  --startup-project src/Beacon.Api

# Review file migration trước khi apply!
# Confirm: không có breaking change, soft delete filter ở OnModelCreating

dotnet ef database update \
  --project src/Beacon.Infrashtructure \
  --startup-project src/Beacon.Api
```

---

## Rules

| Rule | Lý do |
|------|-------|
| **Không vượt ~70 dòng Handler** | Test trước khi viết thêm |
| **Chỉ touch file liên quan đến task** | Không refactor code bên cạnh |
| **Build phải pass sau mỗi increment** | `dotnet build` — 0 error |
| **Test phải GREEN trước khi commit** | Không commit broken test |
| **Business rule trong Domain** | Handler chỉ orchestrate, không chứa logic |
| **`SaveChangesAsync` trong Repository** | Không gọi trong Handler |
| **Commit atomic** | 1 commit = 1 slice hoặc 1 step rõ ràng |

---

## Khi Bị Kẹt

```
1. STOP — Không push qua broken code
2. DIAGNOSE — Gọi /debug để tìm root cause
3. FIX — Fix vấn đề thực sự (không patch symptom)
4. GUARD — Thêm test để prevent recurrence
5. RESUME — Tiếp tục từ step bị kẹt
```

---

## Red Flags — Dừng Và Assess Lại

- ❌ Viết > 70 dòng Handler mà chưa test
- ❌ Mix nhiều slice trong 1 commit
- ❌ Expand scope giữa chừng (scope creep)
- ❌ Build fail giữa các increment
- ❌ Handler inject `AppDbContext` trực tiếp
- ❌ Business logic nằm trong Handler / Controller
- ❌ `SaveChangesAsync` được gọi nhiều lần trong 1 use case
- ❌ Exception throw cho business failure thay vì `Result.Failure`
- ❌ Validator extend `AbstractValidator<DTO>` thay vì `AbstractValidator<Command>`

---

## Output

- Code đúng, tested, đi qua GREEN
- Plan updated — task được đánh `[x]`
- Git history sạch với atomic commits
- `dotnet build` và `dotnet test` đều pass

## Bước Tiếp Theo

Sau khi tất cả slice done:
- Gọi `code-reviewer` — five-axis review
- Gọi `security-auditor` — kiểm tra auth coverage
- Chạy `/review` — final quality check trước merge
