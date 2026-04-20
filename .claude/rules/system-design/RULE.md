# System Design — Beacon

> Rule này chỉ ghi **quyết định thiết kế đã chốt hoặc cần tuân thủ khi implement**. Kiến thức lý thuyết chung (CAP, load balancing, v.v.) không ghi ở đây vì AI đã biết.
> N+1 → `database/RULE.md`. Pagination → `api-conventions/RULE.md`.

---

## CQRS — đã chốt, luôn tuân thủ

Beacon dùng CQRS qua MediatR. **Không bỏ qua pattern này** dù use case đơn giản:

| Operation | Dùng | Nằm ở |
|---|---|---|
| Đọc dữ liệu | `IRequest<Result<TDto>>` Query | `Features/{Module}/Queries/` |
| Ghi/thay đổi | `IRequest<Result<T>>` Command | `Features/{Module}/Commands/` |

- Query **không** được gọi Command và ngược lại.
- Handler **không** được gọi handler khác trực tiếp — dùng repository hoặc service chung.

---

## Background Jobs (⏳ chưa implement — đọc trước khi thêm)

Dùng background job khi: gửi notification, escalation alert, cleanup job, tác vụ chậm không cần kết quả ngay.

**Quyết định chưa chốt** (xem CLAUDE.md § Open Decisions):
- **Hangfire** — persistent job store, dashboard, retry built-in, cần DB table riêng
- **System.Threading.Channels** — in-process, nhẹ hơn, mất job nếu app restart

Khi implement: đặt job definition trong `Application/`, worker trong `Api/Backgroundjobs/`. Không đặt business logic trong worker — worker chỉ dispatch MediatR command.

---

## Caching (⏳ chưa implement — đọc trước khi thêm)

Khi Redis được thêm vào: dùng `IDistributedCache`. **Không inject `IMemoryCache`** vào Application layer (vi phạm layer boundary).

Ưu tiên cache-aside pattern (lazy load):

```csharp
var cached = await cache.GetAsync(key, ct);
if (cached is not null) return Deserialize(cached);

var data = await _repo.GetAsync(...);
await cache.SetAsync(key, Serialize(data), new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) });
return data;
```

Cache key format: `beacon:{module}:{entity}:{id}` — xem `nami-conventions/RULE.md`.

**Invalidation**: xóa cache ngay sau write thành công — đừng dùng TTL làm cơ chế invalidation chính.

---

## Idempotency cho Mobile Client (⏳ chưa implement)

Mobile client hay retry khi mạng chậm. Các endpoint **tạo resource** (register, checkin, upload) phải hỗ trợ `Idempotency-Key` header khi implement:

```
POST /api/v1/checkins
Idempotency-Key: <client-generated-uuid>
```

Store key + response vào Redis với TTL 24h. Nếu key đã tồn tại → trả response cũ, không tạo lại.

---

## Resilience — External Services (MinIO, SQL Server)

Hiện tại không có retry/circuit breaker. Khi thêm: dùng **Polly** (đã là transitive dep của .NET Aspire / HttpClientFactory).

Áp dụng retry cho:
- MinIO upload/download (transient network errors)
- HTTP client gọi ra ngoài (notification, SMS gateway)

**Không áp dụng retry** cho SQL Server write trong handler vì EF transaction đã atomic — retry có thể gây duplicate.

```csharp
// Ví dụ khi dùng IHttpClientFactory + Polly
services.AddHttpClient<ISmsService, SmsService>()
    .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(3, _ => TimeSpan.FromSeconds(2)));
```

---

## Health Checks — pattern đã implement

```
GET /health       → Liveness (service có đang chạy không?)
GET /health/ready → Readiness (SQL Server + MinIO có kết nối không?)
```

Khi thêm dependency mới (Redis, external API): bắt buộc thêm health check tương ứng trong `Api/Extensions/HealthCheckExtensions.cs`.

MinIO health check đã có tại `Api/HealthChecks/MinioHealthCheck.cs` — làm tham chiếu khi implement thêm.

---

## Soft Delete vs Hard Delete

| Trường hợp | Dùng |
|---|---|
| Entity có thể phục hồi / liên quan audit | `SoftDeletableEntity` → `IsDeleted = true` |
| File trên MinIO (hard delete thực sự) | Cần permission `media:hard-delete`, chỉ admin |
| Lookup/reference data không nhạy cảm | Hard delete bình thường |

EF query filter trên `IsDeleted` được apply tự động — truy vấn không cần `Where(x => !x.IsDeleted)` thủ công.
