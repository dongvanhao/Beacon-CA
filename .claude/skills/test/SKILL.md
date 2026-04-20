---
name: Test-Driven Development
description: TDD cho Beacon — xUnit, handler unit tests, WebApplicationFactory integration tests
---

# TDD Skill — Beacon

> Test naming → `nami-conventions/RULE.md`. Coverage floor: 70% Application layer.

---

## RED → GREEN → REFACTOR

```
RED:    Viết test mô tả behavior → chạy → FAIL  ✅
GREEN:  Implement tối thiểu để pass → chạy → PASS ✅
REFACTOR: Cải thiện code, không đổi behavior → chạy → PASS ✅
```

```bash
dotnet test tests/Beacon.UnitTests        # sau mỗi bước
dotnet test tests/Beacon.IntergrationTests
```

---

## Unit Test — Handler Pattern (chuẩn Beacon)

```csharp
// tests/Beacon.UnitTests/Identity/LoginCommandHandlerTests.cs
public class LoginCommandHandlerTests
{
    // Arrange — khởi tạo 1 lần cho cả class
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IJwtService> _jwtService = new();
    private readonly Mock<IUserDeviceRepository> _deviceRepo = new();
    private readonly UserAuthMapper _mapper = new();
    private readonly LoginCommandHandler _sut;

    public LoginCommandHandlerTests()
    {
        _sut = new LoginCommandHandler(_userRepo.Object, _jwtService.Object, _deviceRepo.Object, _mapper);
    }

    [Fact]
    public async Task Handle_WithValidCredentials_ReturnsAuthResponse()
    {
        // Arrange
        var user = BuildUser(isActive: true);
        _userRepo.Setup(r => r.GetByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(user);
        _jwtService.Setup(j => j.GenerateAccessToken(It.IsAny<User>()))
                   .Returns("access-token");

        // Act
        var result = await _sut.Handle(new LoginCommand(new LoginRequest { Username = "john", Password = "pass" }, "ua"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-token");
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsUnauthorizedError()
    {
        _userRepo.Setup(r => r.GetByUsernameAsync(It.IsAny<string>(), default)).ReturnsAsync((User?)null);

        var result = await _sut.Handle(new LoginCommand(new LoginRequest { Username = "ghost" }, "ua"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("INVALID_CREDENTIALS");
    }
}
```

**Quy tắc cấu trúc:**
- 1 handler = 1 test class trong `tests/Beacon.UnitTests/{Module}/`
- Mock **interface** (IRepository, IJwtService) — không mock concrete class
- `_sut` (System Under Test) = instance handler
- Builder method `BuildUser(...)` cho domain object phức tạp — không khởi tạo inline lặp lại

---

## Test Naming

```
{Method}_{Scenario}_{ExpectedResult}

Handle_WithValidCredentials_ReturnsAuthResponse      ✅
Handle_WhenUserNotFound_ReturnsUnauthorizedError      ✅
Handle_WhenAccountInactive_ReturnsUnauthorizedError   ✅
Handle_WithEmptyUsername_ThrowsValidationException    ✅

Test_Login          ❌  (không rõ scenario)
Works               ❌
```

---

## Prove-It Pattern (Bug Fix)

**Bắt buộc**: viết failing test trước khi sửa bug.

```
1. Viết test reproduce bug → chạy → FAIL (xác nhận bug tồn tại)
2. Sửa bug
3. Chạy test → PASS
4. Chạy toàn bộ suite → không có regression
```

```csharp
[Fact]
public async Task Handle_WhenSoftDeletedMedia_ReturnNotFoundError() // Bug #123
{
    var media = BuildMedia(isDeleted: true);
    _mediaRepo.Setup(r => r.GetByIdAsync(media.Id, default)).ReturnsAsync((MediaObject?)null);

    var result = await _sut.Handle(new GetMediaByIdQuery(media.Id, Guid.NewGuid()), default);

    result.IsSuccess.Should().BeFalse();
    result.Error.Code.Should().Be("MEDIA_NOT_FOUND");
}
```

---

## Integration Test — Controller Pattern

```csharp
// tests/Beacon.IntergrationTests/Identity/AuthControllerTests.cs
public class AuthControllerTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Register_WithValidData_Returns201WithAuthResponse()
    {
        var client = factory.CreateClient();
        var request = new RegisterRequest { Username = "john", Email = "john@test.com", Password = "Pass@123" };

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        body!.Success.Should().BeTrue();
        body.Data!.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithInvalidPassword_Returns400WithErrorCode()
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest { Username = "john", Password = "wrong" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body!.Code.Should().Be("INVALID_CREDENTIALS");
    }
}
```

---

## Test Pyramid — Beacon

```
         ┌─────────────────────┐
         │  Integration Tests  │  Beacon.IntergrationTests
         │  Controller + DB    │  WebApplicationFactory
         ├─────────────────────┤
         │    Unit Tests       │  Beacon.UnitTests
         │  Handler + Mapper   │  Mock repositories
         └─────────────────────┘
```

**Unit test**: handler logic, mapper output, validator rules — isolate với Mock.
**Integration test**: HTTP request → response shape, status code, error code.

---

## Test Doubles — Thứ tự ưu tiên

1. **Fake** (in-memory repo) — tốt nhất cho integration logic phức tạp
2. **Mock** (Moq `Mock<IRepository>`) — đủ cho hầu hết handler tests
3. **Stub** — chỉ cần return giá trị cố định, không verify call

**Không mock**: Domain entity, DTO, Mapper (dùng instance thật), `Result<T>`.

---

## Checklist trước khi commit

- [ ] Handler mới có test class tương ứng?
- [ ] Test cover cả happy path lẫn error path (NotFound, Unauthorized, Conflict)?
- [ ] Bug fix có failing test được viết trước?
- [ ] Test name theo pattern `{Method}_{Scenario}_{ExpectedResult}`?
- [ ] Không có `[Fact]` bị `Skip` / comment out?
- [ ] `dotnet test` toàn bộ solution pass?
