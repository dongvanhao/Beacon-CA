---
name: write-unit-test
description: Viết unit test cho Handler/Service vừa tạo trong Beacon API — xUnit + Moq + FluentAssertions
---

## Khi nào dùng

Sau khi implement xong Handler hoặc Service mới. Chạy **trước khi commit**.

Không dùng cho integration test (có project riêng `Beacon.IntergrationTests`).

## Cách gọi

```
/write-unit-test
```
Sau đó cho biết Handler/Service cần test và file path của nó.

## Điều kiện trước khi chạy

Nếu Moq/FluentAssertions chưa cài:
```bash
dotnet add tests/Beacon.UnitTests package Moq
dotnet add tests/Beacon.UnitTests package FluentAssertions
```

## Vị trí file test

```
tests/Beacon.UnitTests/Features/{Module}/{HandlerName}Tests.cs
```

## Quy tắc bắt buộc

**Tên test:** `MethodName_Scenario_ExpectedResult`
Ví dụ: `Handle_WhenEmailExists_ReturnsConflict`

**Cho mỗi Handler, tạo test cho:**
1. Happy path — input hợp lệ → thành công
2. Not found — nếu query theo ID
3. Conflict/business rule fail — nếu có duplicate check
4. Unauthorized/Inactive — nếu có auth check

**Pattern Arrange / Act / Assert:**
```csharp
[Fact]
public async Task Handle_WhenEmailExists_ReturnsConflict()
{
    // Arrange
    var mockRepo = new Mock<IUserRepository>();
    mockRepo.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    var handler = new RegisterCommandHandler(mockRepo.Object, new Mock<IJwtService>().Object);

    // Act
    var result = await handler.Handle(
        new RegisterCommand(new RegisterRequest { Email = "x@x.com", Password = "12345678", FullName = "X" }),
        CancellationToken.None);

    // Assert
    result.IsFailure.Should().BeTrue();
    result.Error.Code.Should().Be(ErrorCodes.Identity.EMAIL_ALREADY_EXISTS);
}
```

## SOLID trong test

| Nguyên tắc | Áp dụng |
|---|---|
| S | Mỗi `[Fact]` test đúng 1 behavior, không gộp 2 scenario |
| I | Chỉ setup mock methods mà Handler **thực sự gọi** |
| D | `new Mock<IUserRepository>()` — KHÔNG `new Mock<UserRepository>()` |

**Mock config-dependent service (không cần appsettings.json):**
```csharp
var mockJwt = new Mock<IJwtService>();
mockJwt.Setup(j => j.GenerateAccessToken(It.IsAny<User>()))
       .Returns(("fake-token", DateTime.UtcNow.AddMinutes(15)));
mockJwt.Setup(j => j.GenerateRefreshToken())
       .Returns(("fake-refresh", DateTime.UtcNow.AddDays(7)));
```

## Không mock

- Domain entities — tạo trực tiếp bằng factory method (`User.Create(...)`)
- `Result<T>` — kiểm tra `.IsSuccess`, `.IsFailure`, `.Error.Code`

## Sau khi viết

```bash
dotnet test tests/Beacon.UnitTests --no-build
```
