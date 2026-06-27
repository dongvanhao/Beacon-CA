# Plan: Serilog Integration + Request Logging

**Module**: Cross-cutting (chỉ `Beacon.Api`)
**Phạm vi**: 4 slices · không schema change · không endpoint mới
**Nguồn spec**: [../specs/serilog-request-response-logging.md](../specs/serilog-request-response-logging.md)

> ⚠️ Feature thuần **infrastructure / cross-cutting** — KHÔNG có Domain Entity, MediatR Handler, Repository, Migration, hay Controller. Template `/plan` chuẩn (Entity→Handler→Repo→Controller) không áp dụng nguyên văn. Giữ tinh thần vertical slice (mỗi slice buildable + verify độc lập), map sang: bootstrap → pure logic (TDD) → wiring → config môi trường.

## Quyết định đã chốt

| # | Quyết định |
|---|---|
| 1 | **Console-only** sink (stdout). Không file sink, không volume Docker. |
| 2 | Seq/Loki/OTel exporter → **ADR riêng**, ngoài scope. |
| 3 | Correlation ID cross-service → **hoãn** (single service, MVP). `TraceId` đặt nền OTel. |
| 4 | `RequestLogging:MaskClientIp` default `false`; có flag mask khi cần GDPR-strict. |
| 5 | `TraceId`/`SpanId` qua `Serilog.Enrichers.Span`. |

## Rules áp dụng

Domain rules (Result/Handler/Repo/Mapper) **không liên quan** — không có business logic. Rule enforce ở slice này:
- Package chỉ thêm vào `Beacon.Api`.
- DI qua extension method — không nhồi `Program.cs`.
- Không log PII (`Authorization`, token, email, phone, password).

---

## Phase 1: Foundation

### Slice 1.1 — Serilog thay thế host logger (bootstrap + base config)

**Type**: Infrastructure wiring · **Dependencies**: không có

**Việc làm:**
1. `Beacon.Api.csproj` — thêm 6 package: `Serilog.AspNetCore`, `Serilog.Sinks.Console`, `Serilog.Enrichers.Environment`, `Serilog.Enrichers.Thread`, `Serilog.Enrichers.Span`, `Serilog.Settings.Configuration`.
2. `Api/Logging/SerilogConfiguration.cs` — `CreateBootstrapLogger()` + `AddSerilogLogging(this WebApplicationBuilder)` (`ReadFrom.Configuration` + `ReadFrom.Services` + `Enrich.FromLogContext`).
3. `Program.cs` — `CreateBootstrapLogger()` dòng đầu; `builder.AddSerilogLogging()`; bọc thân bằng `try / catch(Log.Fatal) / finally(Log.CloseAndFlush)`.
4. `appsettings.json` — section `Serilog` có **Console sink fallback** + `MinimumLevel.Override` (Microsoft/EF/Hangfire/System = Warning) + enrichers + `Properties.Application=Beacon.Api`; section `RequestLogging.MaskClientIp=false`.

**Acceptance:**
- [ ] `dotnet build` — 0 error, 0 warning
- [ ] App boot được (local + `docker compose up`); log startup hiện qua Serilog console
- [ ] Log của `ExceptionHandlingMiddleware`, EF, Hangfire đi qua Serilog (định dạng đồng nhất)
- [ ] Tắt 1 dependency (vd SQL sai) → bootstrap logger vẫn ghi được lỗi startup

---

## ✅ Checkpoint: Foundation
`dotnet build` clean · app chạy được Docker · không còn logger mặc định.

---

## Phase 2: Pure Logic (TDD RED → GREEN)

### Slice 2.1 — `RequestLoggingHelper` + `ResolveLevel` (test trước)

**Type**: Pure logic · **Dependencies**: 1.1 (cần type `LogEventLevel`)

**Bước 1 — Test (RED):** `tests/Beacon.UnitTests/Logging/RequestLoggingHelperTests.cs`
- `IsExcluded_HealthPath_ReturnsTrue`, `IsExcluded_HangfirePath_ReturnsTrue`, `IsExcluded_ApiPath_ReturnsFalse`
- `ResolveUserId_WithNameIdentifier_ReturnsGuid` (dựng claim `ClaimTypes.NameIdentifier` — khớp codebase)
- `ResolveUserId_WithOnlySubClaim_ReturnsGuid`, `ResolveUserId_Anonymous_ReturnsAnonymous`
- `SanitizeQueryString_WithTokenParam_MasksValue`, `_WithMonkeyParam_DoesNotMask` (chống over-match), `_IsCaseInsensitive`
- `MaskIp_WhenEnabled_MasksLastOctet`, `MaskIp_WhenDisabled_ReturnsFull`
- `ResolveLevel_500_ReturnsError`, **`ResolveLevel_WithException_ReturnsError`** ← case blocking đã fix, `_404_ReturnsWarning`, `_200_ReturnsInformation`, `_ExcludedPath_ReturnsVerbose`

**Bước 2 — Implement (GREEN):** `Api/Logging/RequestLoggingHelper.cs` — `IsExcluded`, `ResolveUserId`, `SanitizeQueryString` (exact param-name, case-insensitive), `MaskIp`, `ResolveLevel(statusCode, isExcluded, hasException)`.

**Acceptance:**
- [ ] Toàn bộ unit test GREEN
- [ ] `ResolveLevel` tách rời, không phụ thuộc `HttpContext` → test trực tiếp

---

## ✅ Checkpoint: Logic
Mọi nhánh level + sanitization + claim-resolution có test xanh, độc lập runtime.

---

## Phase 3: Wiring

### Slice 3.1 — `UseSerilogRequestLogging` + middleware order

**Type**: Infrastructure wiring · **Dependencies**: 1.1, 2.1

**Việc làm:**
1. `SerilogConfiguration.UseRequestLogging(this WebApplication, bool maskIp)` — `options.GetLevel` gọi `ResolveLevel`; `options.EnrichDiagnosticContext` set `UserId`/`ClientIp`/`QueryStringSanitized` qua helper.
2. `Program.cs` order: `UseForwardedHeaders` → **`UseRequestLogging`** → `ExceptionHandlingMiddleware` → (giữ nguyên phần còn lại).

**Acceptance (manual/integration smoke):**
- [ ] `dotnet build` clean
- [ ] Endpoint hợp lệ có token → 1 dòng `Information`, có `UserId=<guid>`, `TraceId`
- [ ] Endpoint ném exception → **đúng 1 dòng** `Error` status 500 (request log) **+ đúng 1 dòng** stack (ExceptionHandling) — không double, không thiếu
- [ ] `GET /health`, `/hangfire` → **không** sinh request log (Verbose bị lọc)
- [ ] `?access_token=abc` → log hiện `access_token=***`

---

## Phase 4: Config môi trường

### Slice 4.1 — Dev human-readable / Prod JSON

**Type**: Config · **Dependencies**: 1.1

**Việc làm:**
1. `appsettings.Development.json` — Console output template human-readable (kèm `{TraceId}`), `MinimumLevel.Default=Debug`, EF Core override `Information`.
2. `appsettings.Production.json` — Console JSON formatter (stdout), `MinimumLevel.Default=Information`, EF Core `Warning`. **Không file sink.**

**Acceptance:**
- [ ] Chạy `ASPNETCORE_ENVIRONMENT=Development` → log màu, đọc được
- [ ] `docker compose up` (Production) → `docker compose logs api` ra JSON có `UserId`, `TraceId`, `Application`
- [ ] EF query log: hiện ở Dev, ẩn ở Prod

---

## ✅ Final Checkpoint

- [ ] `dotnet build` — 0 warning
- [ ] `dotnet test tests/Beacon.UnitTests` — GREEN (Slice 2.1)
- [ ] Manual smoke Phase 3 + 4 pass
- [ ] `security-auditor` (tùy chọn): xác nhận không log `Authorization`/token/PII
- [ ] Cập nhật `CLAUDE.md § Open Tech Debt` (Serilog: TODO → ✅)
- [ ] Ready for `/review`

---

## Thứ tự thực thi

```
1.1 (foundation) → 2.1 (TDD pure logic) → 3.1 (wiring) → 4.1 (env config)
```

Risk-first: blocking issue (exception→500) nằm ở **3.1**, nhưng logic của nó (`ResolveLevel` + exception branch) được test cô lập sớm ở **2.1** trước khi wiring — rủi ro khử ngay phase 2.

## Tùy chọn mở rộng

Nếu muốn test Phase 3 chặt hơn: thêm **Slice 3.2** — integration test bằng `WebApplicationFactory` + in-memory Serilog sink để assert log output (thay vì manual smoke).
