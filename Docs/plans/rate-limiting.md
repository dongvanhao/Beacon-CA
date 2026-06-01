# Plan: Rate Limiting

**Spec**: `docs/specs/rate-limiting.md`
**Module**: API Layer only (Domain / Application / Infrastructure không thay đổi)
**Phạm vi**: 5 slices, 3 phases
**Ưu tiên build order**: Foundation → UC1 Auth → UC2 General → UC3 Concurrency → UC5 Proxy

> Rate Limiting là pure API-layer concern. Không có Handler, Repository, EF Migration, hay Domain Entity.
> Template TDD vẫn áp dụng: viết Integration Test (RED) trước, wire middleware/policy (GREEN) sau.

---

## Tổng quan files bị ảnh hưởng

| File | Thay đổi |
|---|---|
| `Beacon.Shared/Constants/ErrorCodes.cs` | Thêm 2 constants |
| `Beacon.Api/Options/RateLimitingOptions.cs` | Tạo mới |
| `Beacon.Api/Extensions/RateLimitingExtensions.cs` | Tạo mới |
| `Beacon.Api/Program.cs` | Thêm service registration + middleware |
| `Beacon.Api/appsettings.json` | Thêm section `RateLimiting` |
| `Beacon.Api/Controllers/Identity/AuthController.cs` | Thêm `[EnableRateLimiting]` trên 5 actions |
| `Beacon.Api/Controllers/Identity/AdminAuthController.cs` | Thêm `[EnableRateLimiting]` trên Login |
| `tests/Beacon.IntergrationTests/Identity/RateLimitingTests.cs` | Tạo mới |

---

## Phase 1: Foundation

### ✅ Slice 1.1 — ErrorCodes + Strongly-Typed Options + Config

**Không có test** — đây là scaffolding thuần túy.

#### Bước 1 — ErrorCodes.cs

File: `src/Beacon.Shared/Constants/ErrorCodes.cs`

Thêm vào cuối nhóm constants hiện có:

```csharp
// Rate Limiting
public const string RATE_LIMIT_EXCEEDED = "RATE_LIMIT_EXCEEDED";
public const string SERVER_BUSY         = "SERVER_BUSY";
```

#### Bước 2 — RateLimitingOptions.cs

File: `src/Beacon.Api/Options/RateLimitingOptions.cs` *(tạo mới, thư mục `Options/` nếu chưa có)*

```csharp
namespace Beacon.Api.Options;

public sealed class RateLimitingOptions
{
    public bool Enabled { get; init; } = true;
    public AuthRateLimitOptions Auth { get; init; } = new();
    public ApiRateLimitOptions Api { get; init; } = new();
    public GlobalRateLimitOptions Global { get; init; } = new();
}

public sealed class AuthRateLimitOptions
{
    public int LoginPermitLimit { get; init; } = 10;
    public int LoginWindowMinutes { get; init; } = 15;
    public int AdminLoginPermitLimit { get; init; } = 5;
    public int AdminLoginWindowMinutes { get; init; } = 15;
    public int RegisterPermitLimit { get; init; } = 5;
    public int RegisterWindowHours { get; init; } = 1;
    public int RefreshTokenPermitLimit { get; init; } = 30;
    public int RefreshTokenWindowMinutes { get; init; } = 15;
    public int CheckEmailPermitLimit { get; init; } = 20;
    public int CheckEmailWindowSeconds { get; init; } = 60;
    public int CheckPhonePermitLimit { get; init; } = 20;
    public int CheckPhoneWindowSeconds { get; init; } = 60;
}

public sealed class ApiRateLimitOptions
{
    public int AuthenticatedPermitLimit { get; init; } = 200;
    public int AuthenticatedWindowMinutes { get; init; } = 1;
    public int AuthenticatedBurst { get; init; } = 50;
    public int AdminPermitLimit { get; init; } = 500;
    public int AdminBurst { get; init; } = 100;
    public int UnauthenticatedPermitLimit { get; init; } = 60;
    public int UnauthenticatedWindowMinutes { get; init; } = 1;
}

public sealed class GlobalRateLimitOptions
{
    public int ConcurrencyLimit { get; init; } = 1000;
    public int QueueLimit { get; init; } = 0;
}
```

#### Bước 3 — appsettings.json

File: `src/Beacon.Api/appsettings.json`

Thêm section `"RateLimiting"` sau section `"Firebase"`:

```json
"RateLimiting": {
  "Enabled": true,
  "Auth": {
    "LoginPermitLimit": 10,
    "LoginWindowMinutes": 15,
    "AdminLoginPermitLimit": 5,
    "AdminLoginWindowMinutes": 15,
    "RegisterPermitLimit": 5,
    "RegisterWindowHours": 1,
    "RefreshTokenPermitLimit": 30,
    "RefreshTokenWindowMinutes": 15,
    "CheckEmailPermitLimit": 20,
    "CheckEmailWindowSeconds": 60,
    "CheckPhonePermitLimit": 20,
    "CheckPhoneWindowSeconds": 60
  },
  "Api": {
    "AuthenticatedPermitLimit": 200,
    "AuthenticatedWindowMinutes": 1,
    "AuthenticatedBurst": 50,
    "AdminPermitLimit": 500,
    "AdminBurst": 100,
    "UnauthenticatedPermitLimit": 60,
    "UnauthenticatedWindowMinutes": 1
  },
  "Global": {
    "ConcurrencyLimit": 1000,
    "QueueLimit": 0
  }
}
```

**Acceptance criteria:**
- [ ] `dotnet build` — 0 error
- [ ] `RateLimitingOptions` bind đúng từ config (verify bằng watch window hoặc unit test nhỏ)

**Dependencies**: Không có

---

## ✅ Checkpoint: Foundation Complete

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] `RateLimitingOptions.cs` tồn tại tại `Api/Options/`
- [ ] 2 constants mới trong `ErrorCodes.cs`
- [ ] `appsettings.json` có section `RateLimiting` đầy đủ

---

## Phase 2: Core Infrastructure + Auth Endpoint Protection (UC1)

### ✅ Slice 2.1 — RateLimitingExtensions + OnRejected + Program.cs + [EnableRateLimiting] cho Auth

#### Bước 1 — Viết Integration Test (RED) trước

File: `tests/Beacon.IntergrationTests/Identity/RateLimitingTests.cs`

Viết skeleton — test **FAIL** vì middleware chưa được wire:

```csharp
public class RateLimitingTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    // UC1-T1: Login vượt limit → 429
    [Fact]
    public async Task Login_WhenExceedLimit_Returns429()
    {
        // Gọi POST /api/v1/auth/login 11 lần cùng IP
        // lần 11 → StatusCode == 429
        // body.code == "RATE_LIMIT_EXCEEDED"
        // header "Retry-After" present
    }

    // UC1-T2: Partition isolation — 2 IP khác nhau không ảnh hưởng nhau
    [Fact]
    public async Task Login_DifferentIPs_HaveIndependentQuota()
    {
        // IP_A gọi 10 lần → OK
        // IP_B gọi 1 lần → 200 (không bị ảnh hưởng bởi IP_A)
    }

    // UC1-T3: check-email vượt limit
    [Fact]
    public async Task CheckEmail_WhenExceedLimit_Returns429() { }

    // UC1-T4: check-phone vượt limit
    [Fact]
    public async Task CheckPhone_WhenExceedLimit_Returns429() { }
}
```

→ Commit với test FAIL — đây là điểm khởi đầu TDD.

#### Bước 2 — RateLimitingExtensions.cs

File: `src/Beacon.Api/Extensions/RateLimitingExtensions.cs` *(tạo mới)*

```csharp
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Beacon.Shared.Constants;
using Beacon.Shared.Common.Responses;
using Beacon.Api.Options;
using System.Text.Json;

namespace Beacon.Api.Extensions;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var opts = configuration
            .GetSection("RateLimiting")
            .Get<RateLimitingOptions>() ?? new RateLimitingOptions();

        if (!opts.Enabled) return services;

        services.AddRateLimiter(limiterOpts =>
        {
            // ── Auth named policies (Sliding Window, IP-based) ───────────────

            limiterOpts.AddSlidingWindowLimiter("auth-login", o =>
            {
                o.PermitLimit       = opts.Auth.LoginPermitLimit;
                o.Window            = TimeSpan.FromMinutes(opts.Auth.LoginWindowMinutes);
                o.SegmentsPerWindow = 3;
                o.QueueLimit        = 0;
            });

            limiterOpts.AddSlidingWindowLimiter("auth-admin-login", o =>
            {
                o.PermitLimit       = opts.Auth.AdminLoginPermitLimit;
                o.Window            = TimeSpan.FromMinutes(opts.Auth.AdminLoginWindowMinutes);
                o.SegmentsPerWindow = 3;
                o.QueueLimit        = 0;
            });

            limiterOpts.AddSlidingWindowLimiter("auth-register", o =>
            {
                o.PermitLimit       = opts.Auth.RegisterPermitLimit;
                o.Window            = TimeSpan.FromHours(opts.Auth.RegisterWindowHours);
                o.SegmentsPerWindow = 2;
                o.QueueLimit        = 0;
            });

            limiterOpts.AddSlidingWindowLimiter("auth-refresh-token", o =>
            {
                o.PermitLimit       = opts.Auth.RefreshTokenPermitLimit;
                o.Window            = TimeSpan.FromMinutes(opts.Auth.RefreshTokenWindowMinutes);
                o.SegmentsPerWindow = 3;
                o.QueueLimit        = 0;
            });

            limiterOpts.AddSlidingWindowLimiter("auth-check", o =>
            {
                o.PermitLimit       = opts.Auth.CheckEmailPermitLimit; // dùng chung cho email + phone
                o.Window            = TimeSpan.FromSeconds(opts.Auth.CheckEmailWindowSeconds);
                o.SegmentsPerWindow = 2;
                o.QueueLimit        = 0;
            });

            // ── General API policy (Token Bucket, per-User/IP) ───────────────
            // Partition key: UserId claim nếu authenticated, ngược lại IP
            // Admin/SuperAdmin nhận bucket lớn hơn

            limiterOpts.AddPolicy("api-general", httpContext =>
            {
                var user = httpContext.User;
                bool isAdmin = user.HasClaim("actor", "admin")
                    || user.IsInRole("SuperAdmin");

                if (user.Identity?.IsAuthenticated == true)
                {
                    var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                                 ?? user.FindFirst("sub")?.Value
                                 ?? "anonymous";

                    int permitLimit = isAdmin ? opts.Api.AdminPermitLimit : opts.Api.AuthenticatedPermitLimit;
                    int burst       = isAdmin ? opts.Api.AdminBurst       : opts.Api.AuthenticatedBurst;

                    return RateLimitPartition.GetTokenBucketLimiter(
                        partitionKey: $"user:{userId}",
                        factory: _ => new TokenBucketRateLimiterOptions
                        {
                            TokenLimit          = permitLimit + burst,
                            ReplenishmentPeriod = TimeSpan.FromMinutes(opts.Api.AuthenticatedWindowMinutes),
                            TokensPerPeriod     = permitLimit,
                            AutoReplenishment   = true,
                            QueueLimit          = 0,
                        });
                }

                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: $"ip:{ip}",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit       = opts.Api.UnauthenticatedPermitLimit,
                        Window            = TimeSpan.FromMinutes(opts.Api.UnauthenticatedWindowMinutes),
                        SegmentsPerWindow = 3,
                        QueueLimit        = 0,
                    });
            });

            // ── Global Concurrency Limiter (UC3) ─────────────────────────────
            limiterOpts.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                RateLimitPartition.GetConcurrencyLimiter(
                    partitionKey: "global",
                    factory: _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = opts.Global.ConcurrencyLimit,
                        QueueLimit  = opts.Global.QueueLimit,
                    }));

            // ── OnRejected: phân biệt 429 (named policy) vs 503 (concurrency) ─
            limiterOpts.OnRejected = async (context, ct) =>
            {
                bool isConcurrencyLimit = context.HttpContext.Response.StatusCode == 503
                    || !context.Lease.TryGetMetadata(MetadataName.RetryAfter, out _);

                int statusCode;
                string code;
                string message;

                if (isConcurrencyLimit)
                {
                    statusCode = StatusCodes.Status503ServiceUnavailable;
                    code       = ErrorCodes.SERVER_BUSY;
                    message    = "Máy chủ đang bận. Vui lòng thử lại sau.";
                }
                else
                {
                    statusCode = StatusCodes.Status429TooManyRequests;
                    code       = ErrorCodes.RATE_LIMIT_EXCEEDED;
                    message    = "Quá nhiều yêu cầu. Vui lòng thử lại sau.";

                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                        context.HttpContext.Response.Headers.RetryAfter =
                            ((int)retryAfter.TotalSeconds).ToString();
                }

                context.HttpContext.Response.StatusCode  = statusCode;
                context.HttpContext.Response.ContentType = "application/json";

                var body = ApiResponse<object>.Failure(
                    message,
                    code,
                    statusCode == 429 ? new[] { $"Retry after {context.HttpContext.Response.Headers.RetryAfter} seconds" } : null);

                await context.HttpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                    ct);
            };
        });

        return services;
    }
}
```

> **Lưu ý OnRejected**: ASP.NET Core set `Response.StatusCode = 503` cho GlobalLimiter trước khi gọi callback. Named policy bị reject thì status chưa được set (mặc định 200) — callback tự set 429. Đây là cách phân biệt đáng tin cậy nhất.

#### Bước 3 — Program.cs

File: `src/Beacon.Api/Program.cs`

**Service registration** (thêm sau `builder.Services.AddHealthChecking(...)`):

```csharp
builder.Services.AddRateLimiting(builder.Configuration);
```

**Middleware pipeline** — thêm `app.UseRateLimiter()` sau `UseAuthentication()`, trước `UseAuthorization()`:

```csharp
app.UseExceptionHandlingMiddleware();
app.UseHttpsRedirection();
app.UseCors(...);
app.UseAuthentication();
app.UseRateLimiter();       // ← thêm vào đây
app.UseAuthorization();
app.MapControllers();
```

> **Critical**: `UseRateLimiter()` phải sau `UseAuthentication()` — UC2 cần `HttpContext.User` claims để partition theo UserId.

#### Bước 4 — [EnableRateLimiting] trên AuthController

File: `src/Beacon.Api/Controllers/Identity/AuthController.cs`

Thêm attribute trên từng action (không thêm ở controller class level):

```csharp
[HttpPost("login")]
[AllowAnonymous]
[EnableRateLimiting("auth-login")]          // ← thêm
public async Task<IActionResult> Login(...)

[HttpPost("register")]
[AllowAnonymous]
[EnableRateLimiting("auth-register")]       // ← thêm
public async Task<IActionResult> Register(...)

[HttpPost("refresh-token")]
[AllowAnonymous]
[EnableRateLimiting("auth-refresh-token")]  // ← thêm
public async Task<IActionResult> RefreshToken(...)

[HttpGet("check-email")]
[AllowAnonymous]
[EnableRateLimiting("auth-check")]          // ← thêm
public async Task<IActionResult> CheckEmail(...)

[HttpGet("check-phone")]
[AllowAnonymous]
[EnableRateLimiting("auth-check")]          // ← thêm (dùng chung policy)
public async Task<IActionResult> CheckPhone(...)
```

> `Logout` và `Me` không cần auth-specific policy — sẽ được cover bởi `api-general` ở Slice 2.2.

#### Bước 5 — [EnableRateLimiting] trên AdminAuthController

File: `src/Beacon.Api/Controllers/Identity/AdminAuthController.cs`

```csharp
[HttpPost("login")]
[AllowAnonymous]
[EnableRateLimiting("auth-admin-login")]    // ← thêm
public async Task<IActionResult> Login(...)
```

> Admin `Logout` không cần auth-specific limit.

#### Bước 6 — Hoàn thiện Integration Test (GREEN)

File: `tests/Beacon.IntergrationTests/Identity/RateLimitingTests.cs`

Implement đủ test cases UC1 (xem skeleton ở Bước 1):

```csharp
// Để test rate limiting, cần:
// 1. Fake IP qua header "X-Forwarded-For" trong test client
// 2. Dùng factory.CreateClient() với custom handler để set header
// 3. Verify response status + body.code + header Retry-After
```

**Acceptance criteria Slice 2.1:**
- [ ] `dotnet build` — 0 error
- [ ] UC1-T1: login 11 lần cùng IP → lần 11 trả 429
- [ ] UC1-T2: IP isolation — IP_B không bị ảnh hưởng bởi IP_A
- [ ] UC1-T3: check-email 21 lần → lần 21 trả 429
- [ ] UC1-T4: check-phone 21 lần → lần 21 trả 429
- [ ] Response 429 có header `Retry-After`
- [ ] Body 429: `{ success: false, code: "RATE_LIMIT_EXCEEDED" }`

**Dependencies**: Slice 1.1

---

### ✅ Slice 2.2 — General API Protection (UC2) + [EnableRateLimiting] trên BaseController

#### Bước 1 — Viết Integration Test (RED) trước

Thêm vào `RateLimitingTests.cs`:

```csharp
// UC2-T1: Authenticated user vượt general limit → 429
[Fact]
public async Task AuthenticatedUser_WhenExceedGeneralLimit_Returns429()
{
    // Gọi GET /api/v1/auth/me với valid token 201 lần
    // lần 201 → 429 RATE_LIMIT_EXCEEDED
}

// UC2-T2: UserId partition isolation
[Fact]
public async Task TwoUsers_SameIP_HaveIndependentQuota()
{
    // user_A gọi 200 lần (hết quota)
    // user_B (cùng IP) gọi 1 lần → 200 OK (quota độc lập)
}

// UC2-T3: Unauthenticated caller vượt IP limit
[Fact]
public async Task UnauthenticatedCaller_WhenExceedIpLimit_Returns429()
{
    // Không có Bearer token, gọi 61 lần cùng IP → lần 61 trả 429
}

// UC2-T4: CORS preflight không bị limit
[Fact]
public async Task CorsPreflightOptions_NotRateLimited()
{
    // OPTIONS request → không trả 429 dù gọi nhiều lần
}
```

#### Bước 2 — [EnableRateLimiting("api-general")] trên BaseController

File: `src/Beacon.Api/Controllers/BaseController.cs`

Thêm attribute ở class level để áp dụng cho tất cả controller kế thừa:

```csharp
[EnableRateLimiting("api-general")]
public abstract class BaseController : ControllerBase
{
    // ...existing code...
}
```

> **Lý do dùng class level cho api-general**: Policy này áp dụng cho tất cả endpoints — đặt ở BaseController là điểm duy nhất thay vì repeat trên mỗi controller. Auth endpoints có `[EnableRateLimiting("auth-xxx")]` ở action level sẽ **override** policy của BaseController.

**Acceptance criteria Slice 2.2:**
- [ ] UC2-T1: authenticated user 201 req → 429
- [ ] UC2-T2: user_A và user_B cùng IP có quota độc lập
- [ ] UC2-T3: unauthenticated 61 req → 429
- [ ] UC2-T4: OPTIONS CORS preflight không bị limit

**Dependencies**: Slice 2.1

---

## ✅ Checkpoint: Core Protection Complete

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] `dotnet test tests/Beacon.IntergrationTests` — tất cả UC1 + UC2 tests GREEN
- [ ] Auth endpoints: 5 actions trên AuthController + 1 action AdminAuthController có `[EnableRateLimiting]`
- [ ] BaseController có `[EnableRateLimiting("api-general")]`
- [ ] `RateLimitingExtensions.cs` register đủ 5 auth policies + 1 api-general + GlobalLimiter

---

## Phase 3: Global Concurrency + Edge Cases (UC3)

### Slice 3.1 — Xác nhận 429 vs 503 + Edge Case Tests

GlobalLimiter (Concurrency) đã được đăng ký trong Slice 2.1. Slice này verify behavior và bổ sung test cases còn thiếu.

#### Bước 1 — Viết Integration Test (RED)

Thêm vào `RateLimitingTests.cs`:

```csharp
// UC3-T1: Global concurrency hit → 503, không phải 429
[Fact]
public async Task GlobalConcurrency_WhenExceeded_Returns503NotRateLimit()
{
    // Gửi 1001 concurrent request
    // Expect: ít nhất 1 request trả 503
    // Body: { code: "SERVER_BUSY" }
    // KHÔNG trả 429 RATE_LIMIT_EXCEEDED
}

// UC3-T2: Enabled = false → tất cả pass through
[Fact]
public async Task RateLimiting_WhenDisabled_AllRequestsPassThrough()
{
    // factory với config "RateLimiting:Enabled" = false
    // Gọi 100 lần → tất cả 200, không có 429
}
```

#### Bước 2 — Kiểm tra OnRejected logic

Verify lại `OnRejected` trong `RateLimitingExtensions.cs`:

- GlobalLimiter reject → `Response.StatusCode` = 503 trước khi vào callback → code path `SERVER_BUSY`
- Named policy reject → `Response.StatusCode` chưa set → default 200 → callback tự set 429 → code path `RATE_LIMIT_EXCEEDED`

Nếu cần, thêm unit test riêng cho `OnRejected` logic.

**Acceptance criteria Slice 3.1:**
- [ ] UC3-T1: concurrency vượt 1000 → 503 với `code: SERVER_BUSY`
- [ ] UC3-T2: `Enabled = false` → tất cả request pass through
- [ ] 503 body không có `Retry-After` header
- [ ] 429 body luôn có `Retry-After` header

**Dependencies**: Slice 2.1

---

## ✅ Checkpoint: Phase 3 Complete

- [ ] `dotnet test` — tất cả GREEN, bao gồm UC3 tests
- [ ] Verify bằng tay: Swagger call login 11 lần → 429 với đúng JSON shape
- [ ] Verify bằng tay: Response 429 có header `Retry-After`

---

## Phase 4: ForwardedHeaders (UC5 — Conditional)

> **Chỉ implement nếu app deploy sau reverse proxy (nginx/Traefik/Docker).**
> Bỏ qua phase này nếu chạy trực tiếp không qua proxy.

### Slice 4.1 — UseForwardedHeaders + KnownProxies

#### Bước 1 — Program.cs

Thêm `UseForwardedHeaders()` là middleware **đầu tiên**:

```csharp
// Phải là middleware ĐẦU TIÊN — trước ExceptionHandlingMiddleware
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    // Thêm IP của proxy/Docker network để tránh IP spoofing
    // KnownProxies = { IPAddress.Parse("172.17.0.0") }
    // Hoặc KnownNetworks cho subnet
});
app.UseExceptionHandlingMiddleware();
// ...
```

#### Bước 2 — appsettings.json (optional)

Nếu cần cấu hình KnownProxies qua config:

```json
"ForwardedHeaders": {
  "KnownProxies": ["172.17.0.1"],
  "ForwardLimit": 1
}
```

**Acceptance criteria Slice 4.1:**
- [ ] Rate limit partition key = IP thật của client, không phải IP của proxy
- [ ] Test: request qua proxy với `X-Forwarded-For: <client-ip>` → partition đúng theo client IP

**Dependencies**: Slice 2.1

---

## ✅ Final Checkpoint

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] `dotnet test tests/Beacon.UnitTests` — GREEN
- [ ] `dotnet test tests/Beacon.IntergrationTests` — GREEN, bao gồm tất cả rate limiting tests
- [ ] Swagger manual test:
  - [ ] Login 11 lần → 429 với `{ success: false, code: "RATE_LIMIT_EXCEEDED", errors: ["Retry after X seconds"] }`
  - [ ] Header `Retry-After` có trong response 429
  - [ ] Response 503 có `{ success: false, code: "SERVER_BUSY" }`
- [ ] `RateLimiting.Enabled: false` → tất cả request pass through (no 429/503)
- [ ] Security review: verify không log IP trong response body

---

## Thứ tự build (TDD)

```
Slice 1.1  →  Slice 2.1  →  Slice 2.2  →  Slice 3.1  →  [Slice 4.1]
Foundation    UC1 Auth      UC2 General   UC3 + Edge     (if proxy)
```

Mỗi slice: **Test (RED) → Implement → Test (GREEN)** — không skip bước test.
