# SPEC: Rate Limiting

## Objective

Bảo vệ Beacon API khỏi brute-force, credential stuffing, enumeration attacks, và abuse bằng cách giới hạn số request theo IP và User ID. ASP.NET Core 8 đã có `Microsoft.AspNetCore.RateLimiting` built-in — không cần thêm NuGet package mới.

---

## Target Users

| Actor | Context |
|---|---|
| Tất cả client (mobile/web) | Subject to rate limits |
| SuperAdmin / Admin | Higher limit (500 req/phút) thay vì bypass — vẫn subject to global limit |
| Unauthenticated caller | Chỉ có IP-based limit |

> **Lý do không bypass hoàn toàn cho SuperAdmin**: Nếu token bị lộ, attacker có thể abuse API không giới hạn. Higher limit đủ cho usecase hợp lệ mà vẫn giữ được bảo vệ.

---

## Core Features & Use Cases

### UC1 — Auth Endpoint Protection (IP-based, Sliding Window)

**Endpoints bị giới hạn:**

| Endpoint | Limit | Window | Lý do |
|---|---|---|---|
| `POST /api/v1/auth/login` | 10 req | 15 phút | Brute-force password |
| `POST /api/v1/admin/auth/login` | 5 req | 15 phút | Admin brute-force |
| `POST /api/v1/auth/register` | 5 req | 1 giờ | Spam account tạo hàng loạt |
| `POST /api/v1/auth/refresh-token` | 30 req | 15 phút | Token rotation abuse |
| `GET /api/v1/auth/check-email` | 20 req | 1 phút | Email enumeration |
| `GET /api/v1/auth/check-phone` | 20 req | 1 phút | Phone enumeration |

**Acceptance criteria:**
- Vượt limit → HTTP 429, body `ApiResponse<T>` với `code: RATE_LIMIT_EXCEEDED`
- Response header (must have): `Retry-After: <seconds>`
- Response header (should have nếu built-in limiter hỗ trợ): `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`
- Partition key = IP thật của client. Nếu app chạy sau reverse proxy: đọc từ `X-Forwarded-For` (xem UC5)

### UC2 — General API Protection (Per User, Token Bucket)

**Áp dụng cho tất cả endpoints còn lại:**

| Caller | Limit | Window | Burst | Partition key |
|---|---|---|---|---|
| Authenticated user (normal) | 200 req | 1 phút | 50 | `UserId` claim |
| Authenticated Admin/SuperAdmin | 500 req | 1 phút | 100 | `UserId` claim |
| Unauthenticated (no Bearer) | 60 req | 1 phút | — | IP |

**Acceptance criteria:**
- Partition key = `UserId` claim nếu `HttpContext.User.Identity.IsAuthenticated`, ngược lại IP
- Rate limiter phải chạy **sau** `UseAuthentication()` để đọc được claims đúng (xem UC6)
- Hai user khác nhau cùng IP không ảnh hưởng quota của nhau

### UC3 — Global Concurrency Limit

- Toàn bộ API: tối đa **1000 concurrent request** (Concurrency Limiter, `QueueLimit = 0`)
- Mục đích: chống DoS đơn giản, bảo vệ database connection pool

**Acceptance criteria:**
- Vượt → HTTP **503** (không phải 429), body `ApiResponse<T>` với `code: SERVER_BUSY`
- `OnRejected` callback **phải phân biệt** loại limiter để trả đúng status code:
  - Named policy bị reject → 429 `RATE_LIMIT_EXCEEDED`
  - Global concurrency limiter bị reject → 503 `SERVER_BUSY`

### UC4 — Cấu hình qua `appsettings.json`

Tất cả thông số limit phải đọc từ config, không hardcode:

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

### UC5 — Forwarded Headers (khi chạy sau reverse proxy)

Nếu app deploy sau nginx/Traefik/Docker reverse proxy:

- `RemoteIpAddress` = IP của proxy, không phải client thật
- Cần `UseForwardedHeaders()` trước toàn bộ middleware để lấy IP đúng

**Acceptance criteria:**
- Cấu hình `ForwardedHeadersOptions` với `ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto`
- Khai báo `KnownProxies` hoặc `KnownNetworks` tránh IP spoofing
- `app.UseForwardedHeaders()` phải là middleware **đầu tiên** trong pipeline

> Nếu không deploy sau proxy (dev/local): bỏ qua UC5, dùng `RemoteIpAddress` trực tiếp.

### UC6 — Middleware Order (bắt buộc)

Đây là constraint kỹ thuật, không phải feature, nhưng sai thứ tự sẽ làm UC2 fail hoàn toàn.

**Thứ tự đúng:**

```
app.UseForwardedHeaders()       // UC5: nếu có proxy — phải là đầu tiên
app.UseExceptionHandlingMiddleware()
app.UseHttpsRedirection()
app.UseCors()
app.UseAuthentication()         // phải trước UseRateLimiter
app.UseRateLimiter()            // đọc HttpContext.User sau khi auth xong
app.UseAuthorization()
app.MapControllers()
```

**Lý do:** `UseRateLimiter()` trước `UseAuthentication()` thì `HttpContext.User.Identity.IsAuthenticated = false` với mọi request, khiến authenticated user bị tính như unauthenticated và không thể đọc `UserId` claim hay check Admin role.

---

## Out of Scope

- Redis-backed distributed rate limiter (in-memory cho MVP)
- IP + email dual-partition cho login (chống targeted brute-force per-account — future work)
- IP blocklist / ban system
- Adaptive rate limiting
- Rate limit per endpoint + per user combo
- Admin dashboard metrics
- Whitelist IP ranges

---

## Technical Approach (Clean Architecture)

### Domain Layer — Không thay đổi

### Application Layer — Không thay đổi

### Infrastructure Layer — Không thay đổi

### API Layer — Thay đổi chính

**1. `RateLimitingOptions.cs`** — strongly-typed config binding:
```
Api/Options/RateLimitingOptions.cs
```

**2. `RateLimitingExtensions.cs`** — extension method:
```
Api/Extensions/RateLimitingExtensions.cs
```

Chứa:
- `AddRateLimiting(IConfiguration)` — đăng ký tất cả policies
- Named policies: `"auth-login"`, `"auth-admin-login"`, `"auth-register"`, `"auth-refresh-token"`, `"auth-check"`, `"api-general"`
- Global limiter: Concurrency via `options.GlobalLimiter`
- `OnRejected` callback phân biệt loại reject:

```csharp
options.OnRejected = async (context, ct) =>
{
    var isGlobalConcurrency = context.Lease.TryGetMetadata(
        MetadataName.RetryAfter, out _) == false
        && context.HttpContext.Response.StatusCode == 503;

    // Hoặc dùng endpoint metadata/policy name để phân biệt
    var statusCode = /* concurrency */ ? 503 : 429;
    var code = statusCode == 503 ? "SERVER_BUSY" : "RATE_LIMIT_EXCEEDED";
    // ...
};
```

**3. Controller decorators:**
```csharp
[HttpPost("login")]
[EnableRateLimiting("auth-login")]
public async Task<IActionResult> Login(...)

[HttpPost("register")]
[EnableRateLimiting("auth-register")]
public async Task<IActionResult> Register(...)
```

**4. `Program.cs`:**
```csharp
builder.Services.AddRateLimiting(builder.Configuration);

// Middleware order (UC6)
app.UseForwardedHeaders(); // nếu có proxy
app.UseExceptionHandlingMiddleware();
app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();      // sau Authentication
app.UseAuthorization();
app.MapControllers();
```

**5. `ErrorCodes.cs`** — thêm 2 constants:
```csharp
public const string RATE_LIMIT_EXCEEDED = "RATE_LIMIT_EXCEEDED";
public const string SERVER_BUSY = "SERVER_BUSY";
```

---

## Response Format

**HTTP 429 — Rate limit hit:**
```json
{
  "success": false,
  "message": "Quá nhiều yêu cầu. Vui lòng thử lại sau.",
  "code": "RATE_LIMIT_EXCEEDED",
  "data": null,
  "errors": ["Retry after 45 seconds"]
}
```

**HTTP 503 — Concurrency limit hit:**
```json
{
  "success": false,
  "message": "Máy chủ đang bận. Vui lòng thử lại sau.",
  "code": "SERVER_BUSY",
  "data": null,
  "errors": null
}
```

**Response headers (429 only):**
```
Retry-After: 45                          ← must have
X-RateLimit-Limit: 10                    ← should have
X-RateLimit-Remaining: 0                 ← should have (nếu built-in limiter expose)
X-RateLimit-Reset: 1717200000            ← should have (nếu built-in limiter expose)
```

> `Retry-After` là bắt buộc. Ba headers còn lại là best-effort — nếu ASP.NET Core built-in limiter không expose chính xác `Remaining`/`Reset` cho loại limiter đang dùng, có thể bỏ trong MVP thay vì fake số liệu.

---

## Testing Strategy

### Unit Tests

- `RateLimitingOptions` binding đọc đúng từ config (tất cả fields)
- `OnRejected` callback trả HTTP 429 với `RATE_LIMIT_EXCEEDED` cho named policy
- `OnRejected` callback trả HTTP 503 với `SERVER_BUSY` cho global concurrency

### Integration Tests

**Auth endpoint limits (UC1):**
- `POST /api/v1/auth/login` 11 lần cùng IP → lần 11 trả 429
- `POST /api/v1/auth/login` 10 lần IP_A, 1 lần IP_B → IP_B trả 200 (partition isolation)
- `GET /api/v1/auth/check-email` 21 req / phút → lần 21 trả 429
- `GET /api/v1/auth/check-phone` 21 req / phút → lần 21 trả 429
- Response 429 có header `Retry-After`

**General API limits (UC2):**
- Authenticated user_A gọi 201 req → lần 201 trả 429
- User_A và user_B cùng IP: user_B gọi 201 req → trả 429 (partition theo UserId, không phải IP)
- Unauthenticated caller gọi 61 req cùng IP → lần 61 trả 429
- Admin user gọi 501 req → lần 501 trả 429 (higher limit nhưng vẫn có limit)

**Global concurrency (UC3):**
- Concurrency vượt 1000 → trả 503 (không phải 429)
- Body response 503 có `code: SERVER_BUSY`

**Middleware order (UC6):**
- Authenticated request bị rate limit phải dùng UserId partition, không phải IP
- Verify: user_A và user_B cùng IP có quota độc lập

**Edge cases:**
- `OPTIONS` CORS preflight request không bị rate limit (không có `[EnableRateLimiting]` trên preflight)
- `RateLimiting.Enabled: false` → tắt hoàn toàn, mọi request pass through

---

## Boundaries

### Always Do
- Dùng `ApiResponse<T>` cho 429 và 503 — không trả plain text
- Đọc config từ `IOptions<RateLimitingOptions>`, không hardcode
- `UseRateLimiter()` sau `UseAuthentication()` — bắt buộc vì UC2 cần claims
- Thêm `RATE_LIMIT_EXCEEDED` và `SERVER_BUSY` vào `ErrorCodes.cs` trước khi dùng
- `[EnableRateLimiting]` tại action level, không tại controller class level
- `OnRejected` phân biệt rõ 429 vs 503

### Ask First
- Nâng cấp sang Redis-backed distributed rate limiter
- IP + email dual-partition cho login
- Khác biệt limit giữa mobile vs web client

### Never Do
- Inject rate limiter vào Application/Domain layer
- `UseRateLimiter()` trước `UseAuthentication()` (UC2 sẽ fail hoàn toàn)
- `UseRateLimiter()` trước `UseCors()` (CORS preflight bị limit)
- Log IP trong error response (PII)
- Fake `X-RateLimit-Remaining`/`X-RateLimit-Reset` nếu không lấy được chính xác

---

## Open Questions Cần Confirm Trước Khi Build

1. **Proxy/Load Balancer**: App có chạy sau reverse proxy (nginx, Traefik, Docker) không? Nếu có → cần UC5 `UseForwardedHeaders()` + cấu hình `KnownProxies`.

2. **Mobile retry behavior**: App mobile có tự retry khi nhận 429 không? Nếu có, cần document `Retry-After` header để mobile đọc đúng.

---

## Next Step

Sau khi confirm open questions, chạy `/plan rate-limiting` để decompose thành vertical slices.
