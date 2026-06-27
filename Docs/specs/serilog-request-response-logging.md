# Feature: Serilog Integration — Structured Logging + Request Logging

## Objective

Thay thế `Microsoft.Extensions.Logging` mặc định bằng **Serilog** (structured logging), và bật **`UseSerilogRequestLogging()`** built-in để ghi một dòng tóm tắt mỗi HTTP request — phục vụ debug, observability, và sẵn sàng OpenTelemetry (correlation qua `Activity`/TraceId) về sau.

> **Quyết định kiến trúc (chốt):** KHÔNG tự viết request-logging middleware. Dùng `UseSerilogRequestLogging()` của `Serilog.AspNetCore`. Lý do: nó đã xử lý exception đúng (log trong `finally`, rethrow), custom level qua `GetLevel`, thêm field qua `EnrichDiagnosticContext` — đủ cho mọi yêu cầu (exclusion, masking, userId, IP) mà không tái phát minh logic dễ sai. Phần tùy biến (lọc path, mask query) đặt trong **helper thuần** để unit-test được.

## Target Users

- **Developers** — debug local & Docker qua console sink (human-readable ở Dev, JSON ở Prod)
- **Ops/Admin** — log tập trung qua stdout (Docker) + file sink rolling
- Không thêm endpoint public; không ảnh hưởng người dùng cuối

---

## Core Features & Use Cases

### UC-1: Serilog thay thế logging provider mặc định

**Acceptance criteria:**
- `builder.Host.UseSerilog(...)` đọc config từ `IConfiguration` (`ReadFrom.Configuration`), không hardcode
- **Bootstrap logger** (`CreateBootstrapLogger()`) được tạo **trước** `builder.Build()` để bắt lỗi giai đoạn startup/DI; sau đó reconfigure từ config
- Log từ EF Core, ASP.NET Core, Hangfire, `ExceptionHandlingMiddleware` đều đi qua Serilog
- Format: **human-readable + màu** ở Development, **JSON (CompactJson hoặc JsonFormatter)** ở Production
- `appsettings.json` (base) **phải có sẵn ít nhất 1 Console sink fallback** — để môi trường mới (vd Staging) quên override vẫn không "im lặng không log"

### UC-2: Request Logging qua `UseSerilogRequestLogging()`

**Acceptance criteria:**
- Một dòng structured log mỗi request, gồm các field (qua `EnrichDiagnosticContext`):
  - `RequestMethod`, `RequestPath` (mặc định của template)
  - `StatusCode`, `Elapsed` (ms) — mặc định của template
  - `QueryStringSanitized` — query string đã mask (xem UC-5); chỉ set khi có query
  - `UserId` — đọc `ClaimTypes.NameIdentifier`, fallback `"sub"`, mặc định `"anonymous"` (KHÔNG log username/email)
  - `ClientIp` — từ `HttpContext.Connection.RemoteIpAddress` **sau** `ForwardedHeaders` (KHÔNG tự parse `X-Forwarded-For`)
  - `TraceId` — từ `Activity.Current?.TraceId` (W3C, OTel-ready), KHÔNG dùng `HttpContext.TraceIdentifier`
- **Không** log request/response body
- **Không** log header nhạy cảm (`Authorization`, `Cookie`, `X-Api-Key`) — `UseSerilogRequestLogging` mặc định không log header, ta không bật thêm
- Level mapping qua `options.GetLevel`:
  - exception != null **hoặc** `StatusCode >= 500` → `Error`
  - `StatusCode >= 400` → `Warning`
  - path bị exclude (`/health*`, `/hangfire*`) → `Verbose` (bị MinimumLevel `Information` lọc bỏ → không log noise)
  - còn lại → `Information`

### UC-3: Middleware order (xử lý blocking issue exception → 500)

**Acceptance criteria:**
- Thứ tự **bắt buộc**:
  ```
  app.UseForwardedHeaders(...)              // IP client thật trước khi log
  app.UseSerilogRequestLogging(...)         // OUTER — thấy StatusCode cuối cùng
  app.UseMiddleware<ExceptionHandlingMiddleware>()   // INNER — bắt & nuốt exception
  ...
  ```
- Khi controller throw: ExceptionHandling (inner) bắt → log exception + stack → set 500 → ghi response → return bình thường. SerilogRequestLogging (outer) thấy completion với `StatusCode=500` → `GetLevel` trả `Error` → ghi 1 dòng summary.
- **Phân vai rõ:** `ExceptionHandlingMiddleware` chịu trách nhiệm log **chi tiết exception + stack trace**; `UseSerilogRequestLogging` chỉ log **dòng summary** (method/path/status/elapsed). Không có khoảng trống, không double-log exception.

### UC-4: Enrichers — context mặc định

**Acceptance criteria:**
- Mọi log entry tự có: `MachineName`, `EnvironmentName`, `Application="Beacon.Api"`, `ThreadId`
- `Enrich.FromLogContext()` bật để diagnostic context & scope hoạt động
- `TraceId`/`SpanId` có mặt để correlate (qua `Serilog.Enrichers.Span` hoặc set thủ công từ `Activity.Current`)

### UC-5: Sanitization & cấu hình môi trường

**Acceptance criteria:**
- **Query masking:** so khớp **tên param** (case-insensitive, **exact match**, KHÔNG substring) với deny-list cấu hình được — mặc định `["token","access_token","key","apikey","secret","password","refresh_token"]`. Param khớp → value thay bằng `***`. (Tránh over-match kiểu `monkey` dính `key`.)
- **IP masking (tùy chọn):** flag `RequestLogging:MaskClientIp` (default `false`). Khi `true`, mask octet cuối IPv4 / nhóm cuối IPv6. Spec ghi nhận **IP là personal data theo GDPR**; lý do được phép log: phục vụ debug/abuse-detection nội bộ, có cơ chế mask khi cần tuân thủ.
- `appsettings.Development.json`: console human-readable, `MinimumLevel.Default=Debug`, EF Core `Information`
- `appsettings.Production.json`: console JSON formatter (Docker stdout), `MinimumLevel.Default=Information`, EF Core `Warning` — **không file sink**

---

## Out of Scope

- Log request/response **body**
- Sink Seq / Loki / Elastic / OpenTelemetry exporter (→ ADR riêng)
- Correlation ID cross-service (chưa multi-service; `TraceId` đã đặt nền OTel)
- Audit log vào DB (đã có `AdminAuditLog` entity)
- Tự viết request-logging middleware (đã loại bỏ — dùng built-in)
- Bật `EnableSensitiveDataLogging` của EF Core (giữ TẮT)

---

## Technical Approach (Clean Architecture)

### NuGet Packages (chỉ thêm vào `Beacon.Api.csproj`)

| Package | Mục đích |
|---|---|
| `Serilog.AspNetCore` | Host, `UseSerilogRequestLogging()`, `ReadFrom.Configuration` |
| `Serilog.Sinks.Console` | Stdout (Docker) — **sink duy nhất** |
| `Serilog.Enrichers.Environment` | `MachineName`, `EnvironmentName` |
| `Serilog.Enrichers.Thread` | `ThreadId` |
| `Serilog.Enrichers.Span` | `TraceId`, `SpanId` từ `Activity` (OTel-ready) |
| `Serilog.Settings.Configuration` | Đọc config từ `appsettings.json` |

> **Console-only (chốt):** KHÔNG dùng file sink. Docker thu log qua stdout (`docker compose logs api`). Tập trung log ra file/Seq/Loki → ADR riêng.

> KHÔNG thêm package vào Domain / Application / Infrastructure.

### Presentation / API Layer

#### `Api/Logging/RequestLoggingHelper.cs` — **pure, testable**

Chứa toàn bộ logic thuần (không phụ thuộc Serilog runtime) để unit-test:

```csharp
public static class RequestLoggingHelper
{
    private static readonly string[] ExcludedPrefixes = ["/health", "/hangfire"];
    private static readonly HashSet<string> SensitiveParams =
        new(StringComparer.OrdinalIgnoreCase)
        { "token", "access_token", "refresh_token", "key", "apikey", "secret", "password" };

    public static bool IsExcluded(PathString path) =>
        ExcludedPrefixes.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));

    public static string ResolveUserId(ClaimsPrincipal? user) =>
        user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? user?.FindFirst("sub")?.Value
        ?? "anonymous";

    public static string SanitizeQueryString(IQueryCollection query) { /* exact param-name match → *** */ }

    public static string? MaskIp(IPAddress? ip, bool mask) { /* mask last octet/group when enabled */ }
}
```

#### `Api/Logging/SerilogConfiguration.cs` — extension methods

```csharp
public static class SerilogConfiguration
{
    // Gọi sớm nhất trong Program.cs — tạo bootstrap logger
    public static void CreateBootstrapLogger() =>
        Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

    // builder.Host.UseSerilog(...) đọc config + enrich + Services
    public static WebApplicationBuilder AddSerilogLogging(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((ctx, services, cfg) => cfg
            .ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());
        return builder;
    }

    // app.UseSerilogRequestLogging với GetLevel + EnrichDiagnosticContext
    public static WebApplication UseRequestLogging(this WebApplication app, bool maskIp) =>
        app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = (httpCtx, _, ex) =>
                RequestLoggingHelper.IsExcluded(httpCtx.Request.Path) ? LogEventLevel.Verbose
                : ex != null || httpCtx.Response.StatusCode >= 500 ? LogEventLevel.Error
                : httpCtx.Response.StatusCode >= 400 ? LogEventLevel.Warning
                : LogEventLevel.Information;

            options.EnrichDiagnosticContext = (diag, httpCtx) =>
            {
                diag.Set("UserId", RequestLoggingHelper.ResolveUserId(httpCtx.User));
                diag.Set("ClientIp", RequestLoggingHelper.MaskIp(httpCtx.Connection.RemoteIpAddress, maskIp));
                if (httpCtx.Request.QueryString.HasValue)
                    diag.Set("QueryStringSanitized", RequestLoggingHelper.SanitizeQueryString(httpCtx.Request.Query));
            };
        });
}
```

> `TraceId`/`SpanId`: dùng `Serilog.Enrichers.Span` (khai báo trong config `Enrich`), không cần set thủ công.

#### `Program.cs` — thay đổi tối thiểu

```csharp
SerilogConfiguration.CreateBootstrapLogger();   // dòng đầu tiên
try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.AddSerilogLogging();
    // ... phần còn lại giữ nguyên ...
    var app = builder.Build();

    app.UseForwardedHeaders(forwardedHeadersOptions);
    app.UseRequestLogging(maskIp: builder.Configuration.GetValue<bool>("RequestLogging:MaskClientIp"));
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    // ... giữ nguyên order còn lại ...
    app.Run();
}
catch (Exception ex) { Log.Fatal(ex, "Beacon.Api terminated unexpectedly"); }
finally { Log.CloseAndFlush(); }
```

### Configuration

**`appsettings.json`** (base — có sink fallback):
```json
{
  "RequestLogging": { "MaskClientIp": false },
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Enrichers.Environment",
              "Serilog.Enrichers.Thread", "Serilog.Enrichers.Span"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "Microsoft.AspNetCore": "Warning",
        "Hangfire": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [{ "Name": "Console" }],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId",
               "WithEnvironmentName", "WithSpan"],
    "Properties": { "Application": "Beacon.Api" }
  }
}
```

- **`appsettings.Development.json`** — Console output template human-readable (có `{TraceId}`), `MinimumLevel.Default=Debug`, override `Microsoft.EntityFrameworkCore=Information`.
- **`appsettings.Production.json`** — Console JSON formatter (Docker stdout). **Không file sink.**

### Middleware Order (sau khi thêm)

```
ForwardedHeaders
UseSerilogRequestLogging          ← OUTER, sau ForwardedHeaders (đọc IP đúng + StatusCode cuối)
ExceptionHandlingMiddleware       ← INNER, log stack + set 500
UseSwaggerDocs
UseHttpsRedirection
UseCors
UseAuthentication
UseRateLimiter
UseAuthorization
MapControllers
```

### Docker

- **Không thay đổi `docker-compose.yml`.** Console sink ghi ra stdout; xem qua `docker compose logs api`. Không mount volume.

---

## Security & Privacy Constraints

| ❌ Không log | ✅ Thay thế / Biện pháp |
|---|---|
| Email, phone, username | `UserId` (GUID từ `NameIdentifier`) |
| `Authorization` / `Cookie` header | — (request logging không log header) |
| Request/Response body | — |
| Password, token value | — |
| Query param nhạy cảm (exact-name match) | mask `***` qua `SanitizeQueryString` |
| IP (PII/GDPR) | log mặc định (debug nội bộ); mask được qua `RequestLogging:MaskClientIp` |

**Lưu ý về "không PII":** đây là **best-effort ở tầng request logging**, KHÔNG phải cam kết toàn hệ thống. Các nguồn rò rỉ khác được kiểm soát bằng: EF Core giữ `EnableSensitiveDataLogging=false` + level `Warning` ở Prod; exception message do `ExceptionHandlingMiddleware` kiểm soát (không echo input thô); không bật Microsoft request/response body logging.

---

## Testing Strategy

### Unit Tests (`Beacon.UnitTests/Logging/RequestLoggingHelperTests.cs`)

Test **logic thuần** (giá trị cao hơn test middleware built-in):

- `IsExcluded_HealthPath_ReturnsTrue` / `IsExcluded_HangfirePath_ReturnsTrue` / `IsExcluded_ApiPath_ReturnsFalse`
- `ResolveUserId_WithNameIdentifier_ReturnsGuid` (claim dựng đúng `ClaimTypes.NameIdentifier`)
- `ResolveUserId_WithOnlySubClaim_ReturnsGuid` (fallback)
- `ResolveUserId_Anonymous_ReturnsAnonymous`
- `SanitizeQueryString_WithTokenParam_MasksValue`
- `SanitizeQueryString_WithMonkeyParam_DoesNotMask` (chống over-match `key`⊄`monkey`)
- `SanitizeQueryString_IsCaseInsensitive`
- `MaskIp_WhenEnabled_MasksLastOctet` / `MaskIp_WhenDisabled_ReturnsFull`

### GetLevel mapping

Tách `GetLevel` thành hàm tĩnh `ResolveLevel(int statusCode, bool isExcluded, bool hasException)` để test:
- `ResolveLevel_500_ReturnsError`
- `ResolveLevel_WithException_ReturnsError` ← **case blocking đã fix**
- `ResolveLevel_404_ReturnsWarning`
- `ResolveLevel_200_ReturnsInformation`
- `ResolveLevel_ExcludedPath_ReturnsVerbose`

### Integration / Manual Verification

- `docker compose up` → `docker compose logs api` → thấy JSON structured log có `TraceId`, `UserId`
- `GET /health` → **không** xuất hiện dòng request log (Verbose bị lọc)
- Gọi endpoint hợp lệ có token → log `Information`, `UserId=<guid>`
- Gọi endpoint ném exception → **đúng 1 dòng** `Error` status 500 từ request logging + **đúng 1 dòng** chi tiết stack từ `ExceptionHandlingMiddleware` (không double, không thiếu)
- Gọi `?access_token=abc` → log hiển thị `access_token=***`

---

## File Checklist

```
src/Beacon.Api/
  Beacon.Api.csproj                          ← +6 package Serilog (Console-only, không Sinks.File)
  Program.cs                                 ← bootstrap logger, AddSerilogLogging, UseRequestLogging, try/finally
  Logging/
    SerilogConfiguration.cs                  ← [MỚI] extensions
    RequestLoggingHelper.cs                  ← [MỚI] logic thuần testable

src/Beacon.Api/appsettings.json              ← +RequestLogging, +Serilog (Console fallback)
src/Beacon.Api/appsettings.Development.json  ← Serilog overrides (human-readable, Debug)
src/Beacon.Api/appsettings.Production.json   ← +Console JSON formatter

src/tests/Beacon.UnitTests/
  Logging/RequestLoggingHelperTests.cs       ← [MỚI]
```

> `docker-compose.yml` KHÔNG đổi.

---

## Boundaries

### Always Do
- Giữ package Serilog **chỉ** ở `Beacon.Api`.
- Đặt logic tùy biến trong helper thuần để unit-test.
- `Log.CloseAndFlush()` trong `finally` để flush sink khi shutdown.

### Ask First
- Thêm sink mạng (Seq/Loki) hoặc OTel exporter.
- Thay đổi `MinimumLevel` của EF Core ở Production (rủi ro rò PII nếu hạ xuống Information/Debug).

### Never Do
- Bật `EnableSensitiveDataLogging` của EF Core.
- Log body, `Authorization` header, hoặc value token/password.
- Tự viết song song một request-logging middleware khác (→ double log).

---

## Decisions (đã chốt)

1. **Sink** — **Console-only** (stdout). Không file sink, không volume Docker. Tập trung log → ADR riêng.
2. **Seq/Loki/OTel exporter** — **ADR riêng**, ngoài scope slice này.
3. **Correlation ID cross-service** — **hoãn**. Chấp nhận được khi đang là single service, traffic thấp, MVP. `TraceId` (Activity) đã đặt nền cho OTel sau này.
4. **IP logging** — `MaskClientIp` default `false` (log IP đầy đủ cho debug nội bộ); có flag bật mask khi cần GDPR-strict.
5. **`TraceId` enricher** — dùng `Serilog.Enrichers.Span`.
```
