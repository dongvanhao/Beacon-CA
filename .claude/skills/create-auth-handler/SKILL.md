---
name: create-auth-handler
description: Tạo Auth handlers (Register, Login, Logout) cho Identity module trong Beacon API — bao gồm IJwtService, IUserRepository, Commands, Validators, Controller, và DI wiring
---

# Skill: Tạo Auth Handlers (Register / Login / Logout)

## Khi nào dùng skill này

Dùng thay cho `/create-endpoint` khi feature là **Authentication** — vì Auth không phải CRUD thuần:
- Cần password hashing (BCrypt)
- Cần JWT generation (access token + refresh token)
- Cần refresh token rotation logic
- Domain entities (`User`, `RefreshToken`, `UserDevice`) **đã tồn tại sẵn** — KHÔNG tạo lại

---

## Trạng thái codebase hiện tại (đã có sẵn — KHÔNG tạo lại)

### Domain Entities (`src/Beacon.Domain/Entities/Identity/`)
- `User` : `AuditableEntity` — có `Email`, `PasswordHash`, `FullName`, `PhoneNumber`, `Role`, `IsActive`, `IsEmailVerified`, `LastLoginAtUtc`
  - Methods: `User.Create(email, passwordHash, fullName, phoneNumber?)`, `RecordLogin()`, `UpdatePassword(hash)`, `Deactivate()`
- `RefreshToken` : `AuditableEntity` — có `UserId`, `Token`, `ExpiresAtUtc`, `RevokedAtUtc`, `CreatedByIp`, `UserDeviceId`
  - Methods: `RefreshToken.Create(userId, token, expiresAtUtc, createdByIp?, userDeviceId?)`, `Revoke(ip?, replacedByToken?)`
  - Props: `IsExpired`, `IsRevoked`, `IsActive`
- `UserDevice` : `SoftDeletableEntity` — có `UserId`, `Platform`, `DeviceName`, `DeviceToken`, `IsActive`

### Error Codes (`Beacon.Shared.Constants.ErrorCodes.Identity`)
```csharp
ErrorCodes.Identity.USER_NOT_FOUND       // "USER_NOT_FOUND"
ErrorCodes.Identity.EMAIL_ALREADY_EXISTS // "EMAIL_ALREADY_EXISTS"
ErrorCodes.Identity.INVALID_CREDENTIALS  // "INVALID_CREDENTIALS"
ErrorCodes.Identity.TOKEN_EXPIRED        // "TOKEN_EXPIRED"
ErrorCodes.Identity.TOKEN_INVALID        // "TOKEN_INVALID"
ErrorCodes.Identity.ACCOUNT_INACTIVE     // "ACCOUNT_INACTIVE"
```

### Result Pattern
```csharp
Result<T>.Success(value)
Result<T>.Failure(Error.NotFound(code, message))
Result<T>.Failure(Error.Conflict(code, message))
Result<T>.Failure(Error.Unauthorized(code, message))
Result.Success()
Result.Failure(Error.Failure(code, message))
```

---

## Thứ tự tạo (BẮT BUỘC)

```
1. IUserRepository          → src/Beacon.Domain/IRepository/
2. IJwtService              → src/Beacon.Application/Common/Interfaces/IService/
3. DTOs                     → src/Beacon.Application/Features/Identity/Dtos/
4. Validators               → src/Beacon.Application/Features/Identity/Validators/
5. Commands + Handlers      → src/Beacon.Application/Features/Identity/Commands/
6. UserRepository impl      → src/Beacon.Infrashtructure/Repository/Identity/
7. JwtService impl          → src/Beacon.Infrashtructure/Services/
8. DI registration          → src/Beacon.Api/Program.cs
9. AuthController           → src/Beacon.Api/Controllers/
```

---

## Bước 1: IUserRepository

`src/Beacon.Domain/IRepository/IUserRepository.cs`

```csharp
using Beacon.Domain.Entities.Identity;

namespace Beacon.Domain.IRepository;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task AddRefreshTokenAsync(RefreshToken token, CancellationToken ct = default);
    Task<RefreshToken?> GetActiveRefreshTokenAsync(string token, CancellationToken ct = default);

    /// <summary>Dùng cho refresh flow: load kèm User để tái sử dụng claims.</summary>
    Task<RefreshToken?> GetActiveRefreshTokenWithUserAsync(string token, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
```

---

## Bước 2: IJwtService

`src/Beacon.Application/Common/Interfaces/IService/IJwtService.cs`

```csharp
using Beacon.Domain.Entities.Identity;

namespace Beacon.Application.Common.Interfaces.IService;

public interface IJwtService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    int GetAccessTokenExpiryMinutes();
}
```

---

## Bước 3: DTOs

`src/Beacon.Application/Features/Identity/Dtos/RegisterRequest.cs`
```csharp
namespace Beacon.Application.Features.Identity.Dtos;

public class RegisterRequest
{
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? PhoneNumber { get; set; }
}
```

`src/Beacon.Application/Features/Identity/Dtos/LoginRequest.cs`
```csharp
namespace Beacon.Application.Features.Identity.Dtos;

public class LoginRequest
{
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
}
```

`src/Beacon.Application/Features/Identity/Dtos/AuthResponse.cs`
```csharp
namespace Beacon.Application.Features.Identity.Dtos;

public class AuthResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string AccessToken { get; set; } = default!;
    public string RefreshToken { get; set; } = default!;
    public DateTime AccessTokenExpiresAt { get; set; }
}
```

---

## Bước 4: Validators

`src/Beacon.Application/Features/Identity/Validators/RegisterRequestValidator.cs`
```csharp
using Beacon.Application.Features.Identity.Dtos;
using FluentValidation;

namespace Beacon.Application.Features.Identity.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(100).WithMessage("Password must not exceed 100 characters.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(200).WithMessage("Full name must not exceed 200 characters.");

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("Invalid phone number format.")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));
    }
}
```

`src/Beacon.Application/Features/Identity/Validators/LoginRequestValidator.cs`
```csharp
using Beacon.Application.Features.Identity.Dtos;
using FluentValidation;

namespace Beacon.Application.Features.Identity.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
```

> Validators auto-discovered qua `AddValidatorsFromAssembly` — KHÔNG đăng ký thủ công.

---

## Bước 5: Commands + Handlers

### RegisterCommand

`src/Beacon.Application/Features/Identity/Commands/RegisterCommand.cs`
```csharp
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public record RegisterCommand(RegisterRequest Request) : IRequest<Result<AuthResponse>>;
```

`src/Beacon.Application/Features/Identity/Commands/RegisterCommandHandler.cs`
```csharp
using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Identity.Commands;
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public class RegisterCommandHandler(
    IUserRepository userRepository,
    IJwtService jwtService) : IRequestHandler<RegisterCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(RegisterCommand command, CancellationToken ct)
    {
        var req = command.Request;

        // 1. Check email conflict
        if (await userRepository.ExistsByEmailAsync(req.Email, ct))
            return Result<AuthResponse>.Failure(
                Error.Conflict(ErrorCodes.Identity.EMAIL_ALREADY_EXISTS, "Email is already registered."));

        // 2. Hash password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);

        // 3. Create user
        var user = User.Create(req.Email, passwordHash, req.FullName, req.PhoneNumber);
        await userRepository.AddAsync(user, ct);

        // 4. Generate tokens
        var accessToken = jwtService.GenerateAccessToken(user);
        var refreshTokenValue = jwtService.GenerateRefreshToken();
        var refreshToken = RefreshToken.Create(
            userId: user.Id,
            token: refreshTokenValue,
            expiresAtUtc: DateTime.UtcNow.AddDays(7));

        await userRepository.AddRefreshTokenAsync(refreshToken, ct);
        await userRepository.SaveChangesAsync(ct);

        return Result<AuthResponse>.Success(new AuthResponse
        {
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(jwtService.GetAccessTokenExpiryMinutes())
        });
    }
}
```

### LoginCommand

`src/Beacon.Application/Features/Identity/Commands/LoginCommand.cs`
```csharp
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public record LoginCommand(LoginRequest Request) : IRequest<Result<AuthResponse>>;
```

`src/Beacon.Application/Features/Identity/Commands/LoginCommandHandler.cs`
```csharp
using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public class LoginCommandHandler(
    IUserRepository userRepository,
    IJwtService jwtService) : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(LoginCommand command, CancellationToken ct)
    {
        var req = command.Request;

        // 1. Find user
        var user = await userRepository.GetByEmailAsync(req.Email, ct);
        if (user is null)
            return Result<AuthResponse>.Failure(
                Error.Unauthorized(ErrorCodes.Identity.INVALID_CREDENTIALS, "Invalid email or password."));

        // 2. Check account status
        if (!user.IsActive)
            return Result<AuthResponse>.Failure(
                Error.Unauthorized(ErrorCodes.Identity.ACCOUNT_INACTIVE, "Account is inactive."));

        // 3. Verify password
        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Result<AuthResponse>.Failure(
                Error.Unauthorized(ErrorCodes.Identity.INVALID_CREDENTIALS, "Invalid email or password."));

        // 4. Record login timestamp
        user.RecordLogin();

        // 5. Generate tokens
        var accessToken = jwtService.GenerateAccessToken(user);
        var refreshTokenValue = jwtService.GenerateRefreshToken();
        var refreshToken = RefreshToken.Create(
            userId: user.Id,
            token: refreshTokenValue,
            expiresAtUtc: DateTime.UtcNow.AddDays(7));

        await userRepository.AddRefreshTokenAsync(refreshToken, ct);
        await userRepository.SaveChangesAsync(ct);

        return Result<AuthResponse>.Success(new AuthResponse
        {
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(jwtService.GetAccessTokenExpiryMinutes())
        });
    }
}
```

### LogoutCommand

`src/Beacon.Application/Features/Identity/Commands/LogoutCommand.cs`
```csharp
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public record LogoutCommand(string RefreshToken) : IRequest<Result>;
```

`src/Beacon.Application/Features/Identity/Commands/LogoutCommandHandler.cs`
```csharp
using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public class LogoutCommandHandler(IUserRepository userRepository) : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand command, CancellationToken ct)
    {
        var token = await userRepository.GetActiveRefreshTokenAsync(command.RefreshToken, ct);
        if (token is null)
            return Result.Failure(
                Error.NotFound(ErrorCodes.Identity.TOKEN_INVALID, "Refresh token not found or already revoked."));

        token.Revoke();
        await userRepository.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

### RefreshTokenCommand

`src/Beacon.Application/Features/Identity/Commands/RefreshTokenCommand.cs`
```csharp
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public record RefreshTokenCommand(string RefreshToken) : IRequest<Result<AuthResponse>>;
```

`src/Beacon.Application/Features/Identity/Commands/RefreshTokenCommandHandler.cs`
```csharp
using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Commands;

public class RefreshTokenCommandHandler(
    IUserRepository userRepository,
    IJwtService jwtService) : IRequestHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand command, CancellationToken ct)
    {
        // 1. Tìm refresh token còn active và load kèm User
        var existingToken = await userRepository.GetActiveRefreshTokenWithUserAsync(command.RefreshToken, ct);
        if (existingToken is null)
            return Result<AuthResponse>.Failure(
                Error.Unauthorized(ErrorCodes.Identity.TOKEN_INVALID, "Refresh token is invalid or expired."));

        var user = existingToken.User!;

        // 2. Kiểm tra tài khoản
        if (!user.IsActive)
            return Result<AuthResponse>.Failure(
                Error.Unauthorized(ErrorCodes.Identity.ACCOUNT_INACTIVE, "Account is inactive."));

        // 3. Revoke token cũ (rotation)
        existingToken.Revoke();

        // 4. Sinh token mới
        var newAccessToken = jwtService.GenerateAccessToken(user);
        var newRefreshTokenValue = jwtService.GenerateRefreshToken();
        var newRefreshToken = RefreshToken.Create(
            userId: user.Id,
            token: newRefreshTokenValue,
            expiresAtUtc: DateTime.UtcNow.AddDays(7));

        await userRepository.AddRefreshTokenAsync(newRefreshToken, ct);
        await userRepository.SaveChangesAsync(ct);

        return Result<AuthResponse>.Success(new AuthResponse
        {
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            AccessToken = newAccessToken,
            RefreshToken = newRefreshTokenValue,
            AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(jwtService.GetAccessTokenExpiryMinutes())
        });
    }
}
```

> **Lưu ý:** `existingToken.User` yêu cầu navigation property `User` trên `RefreshToken` entity phải tồn tại. Xác nhận trong `RefreshToken` class có `public User? User { get; private set; }` trước khi build.

---

## Bước 6: UserRepository Implementation

`src/Beacon.Infrashtructure/Repository/Identity/UserRepository.cs`
```csharp
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository;
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Identity;

public class UserRepository(AppDbContext context) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await context.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
        => await context.Users.AnyAsync(u => u.Email == email, ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await context.Users.AddAsync(user, ct);

    public async Task AddRefreshTokenAsync(RefreshToken token, CancellationToken ct = default)
        => await context.RefreshTokens.AddAsync(token, ct);

    public async Task<RefreshToken?> GetActiveRefreshTokenAsync(string token, CancellationToken ct = default)
        => await context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == token
                && rt.RevokedAtUtc == null
                && rt.ExpiresAtUtc > DateTime.UtcNow, ct);

    public async Task<RefreshToken?> GetActiveRefreshTokenWithUserAsync(string token, CancellationToken ct = default)
        => await context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == token
                && rt.RevokedAtUtc == null
                && rt.ExpiresAtUtc > DateTime.UtcNow, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
```

> `context.Users` và `context.RefreshTokens` — xác nhận `DbSet` đã tồn tại trong `AppDbContext` trước khi build.

---

## Bước 7: JwtService Implementation

Package cần cài: `dotnet add src/Beacon.Infrashtructure package Microsoft.IdentityModel.Tokens`
và `dotnet add src/Beacon.Infrashtructure package System.IdentityModel.Tokens.Jwt`

`src/Beacon.Infrashtructure/Services/JwtService.cs`
```csharp
using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.Entities.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Beacon.Infrashtructure.Services;

public class JwtService(IConfiguration configuration) : IJwtService
{
    public string GenerateAccessToken(User user)
    {
        var jwtSettings = configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"]!;
        var issuer = jwtSettings["Issuer"]!;
        var audience = jwtSettings["Audience"]!;
        var expiryMinutes = int.Parse(jwtSettings["ExpiryMinutes"] ?? "15");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    public int GetAccessTokenExpiryMinutes()
    {
        var jwtSettings = configuration.GetSection("JwtSettings");
        return int.Parse(jwtSettings["ExpiryMinutes"] ?? "15");
    }
}
```

`appsettings.json` — thêm section:
```json
"JwtSettings": {
  "SecretKey": "your-super-secret-key-at-least-32-characters-long",
  "Issuer": "BeaconAPI",
  "Audience": "BeaconClient",
  "ExpiryMinutes": "15"
}
```

---

## Bước 8: DI Registration

Thêm vào `src/Beacon.Api/Program.cs` (trước `var app = builder.Build()`):

```csharp
// Auth
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IJwtService, JwtService>();

// JWT Bearer middleware
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSettings = builder.Configuration.GetSection("JwtSettings");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!)),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });
```

Và trong pipeline (sau `app.UseMiddleware<ExceptionHandlingMiddleware>()`):
```csharp
app.UseAuthentication();   // TRƯỚC UseAuthorization
app.UseAuthorization();
```

Package cần cài cho API project:
```bash
dotnet add src/Beacon.Api package Microsoft.AspNetCore.Authentication.JwtBearer
```

---

## Bước 9: AuthController

`src/Beacon.Api/Controllers/AuthController.cs`
```csharp
using Beacon.Application.Features.Identity.Commands;
using Beacon.Application.Features.Identity.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers;

[Route("api/v1/auth")]
public class AuthController(IMediator mediator) : BaseController
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
        => CreatedResult("api/v1/auth/me", await mediator.Send(new RegisterCommand(request), ct));

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
        => HandleResult(await mediator.Send(new LoginCommand(request), ct));

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
        => HandleResult(await mediator.Send(new RefreshTokenCommand(request.RefreshToken), ct));

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
        => HandleResult(await mediator.Send(new LogoutCommand(request.RefreshToken), ct));
}
```

`src/Beacon.Application/Features/Identity/Dtos/LogoutRequest.cs`
```csharp
namespace Beacon.Application.Features.Identity.Dtos;

public class LogoutRequest
{
    public string RefreshToken { get; set; } = default!;
}
```

`src/Beacon.Application/Features/Identity/Dtos/RefreshTokenRequest.cs`
```csharp
namespace Beacon.Application.Features.Identity.Dtos;

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = default!;
}
```

---

## Package dependencies tóm tắt

```bash
# BCrypt — cài vào Application hoặc Infrastructure
dotnet add src/Beacon.Application package BCrypt.Net-Next

# JWT — cài vào Infrastructure
dotnet add src/Beacon.Infrashtructure package Microsoft.IdentityModel.Tokens
dotnet add src/Beacon.Infrashtructure package System.IdentityModel.Tokens.Jwt

# JWT Bearer middleware — cài vào API
dotnet add src/Beacon.Api package Microsoft.AspNetCore.Authentication.JwtBearer
```

---

## Security notes

- **Không trả message khác nhau** cho "email không tồn tại" vs "sai password" — cùng trả `INVALID_CREDENTIALS` để tránh user enumeration
- **Không log password** dù là plaintext hay hash
- `SecretKey` trong `appsettings.json` chỉ dùng cho dev — production phải dùng environment variable / secrets manager
- Refresh token dùng `RandomNumberGenerator` (cryptographically secure) — KHÔNG dùng `Guid.NewGuid()`

---

## Gotchas (dễ quên)

- `app.UseAuthentication()` phải đứng **trước** `app.UseAuthorization()` trong pipeline
- `[AllowAnonymous]` trên Register/Login/Refresh — nếu không có, JWT middleware block request ngay cả khi chưa login
- `ClockSkew = TimeSpan.Zero` — nếu không set, token hết hạn vẫn valid thêm 5 phút (mặc định)
- BCrypt có thể cài vào `Beacon.Application` (vì Handler gọi trực tiếp) hoặc tách thành `IPasswordHasher` interface — tùy độ phức tạp của project
- Kiểm tra `context.RefreshTokens` DbSet tồn tại trong `AppDbContext` trước khi build — nếu thiếu sẽ lỗi runtime
- `RefreshToken.User` navigation property phải tồn tại để `Include(rt => rt.User)` hoạt động trong refresh flow

---

## Kiểm soát đầu ra (BẮT BUỘC trước khi commit)

> Với **mỗi file** AI tạo ra, bạn phải trả lời được 3 câu sau. Nếu không trả lời được → yêu cầu giải thích trước khi commit.

**Câu 1 — File này làm gì?**

| File | Phải hiểu rõ |
|---|---|
| `IUserRepository.cs` | Contract truy vấn/lưu User và RefreshToken — Domain layer không phụ thuộc EF Core |
| `IJwtService.cs` | Contract sinh access token, refresh token, và lấy thời gian hết hạn từ config |
| `RegisterCommandHandler.cs` | Check email conflict → hash BCrypt → tạo User → sinh tokens → lưu 1 lần |
| `LoginCommandHandler.cs` | Tìm user → check IsActive → verify BCrypt → ghi LastLoginAtUtc → sinh tokens |
| `LogoutCommandHandler.cs` | Tìm refresh token active → gọi `Revoke()` → lưu DB |
| `RefreshTokenCommandHandler.cs` | Tìm token active kèm User → Revoke cũ → sinh cặp token mới (rotation) |
| `UserRepository.cs` | EF Core impl của `IUserRepository` — query trực tiếp `AppDbContext` |
| `JwtService.cs` | Đọc `JwtSettings` từ config → ký HMAC-SHA256 → sinh refresh token bằng CSPRNG |
| `AuthController.cs` | HTTP layer — nhận request, gửi MediatR command, trả `ApiResponse<T>` |

**Câu 2 — Nó phụ thuộc vào class/interface nào?**

Trước khi chấp nhận file, vẽ nhanh dependency graph:
```
Handler → Interface (Domain/Application) → Implementation (Infrastructure)
Controller → IMediator → Handler
```

**Câu 3 — Nếu xóa file này, lỗi xảy ra ở đâu?**

Ví dụ: xóa `IUserRepository` → build error tại 3 handlers + `UserRepository`. Xóa `UserRepository` → runtime DI error. Nếu không thể trả lời câu này, bạn chưa hiểu đủ để commit.

---

## Sau khi sinh code — bước tiếp theo

```bash
# 1. Build — phải clean
dotnet build

# 2. Viết unit test cho các handlers vừa tạo
/write-unit-test
# Handlers cần test: RegisterCommandHandler, LoginCommandHandler,
#                    LogoutCommandHandler, RefreshTokenCommandHandler

# 3. Chạy test
dotnet test --no-build

# 4. Commit chỉ khi build + test đều pass
# feat(identity): add Register/Login/Logout/RefreshToken handlers with JWT auth
```
