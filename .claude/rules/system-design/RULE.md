# System Design — Beacon

> Chỉ ghi **quyết định đã chốt** hoặc cần tuân thủ khi implement. N+1 → `database/`. Pagination → `api-conventions/`.

---

## CQRS (đã chốt)

Beacon dùng CQRS qua MediatR. **Không bỏ qua** dù use case đơn giản.

| Operation | Dùng | Vị trí |
|---|---|---|
| Đọc | `IRequest<Result<TDto>>` Query | `Features/{M}/Queries/` |
| Ghi/thay đổi | `IRequest<Result<T>>` Command | `Features/{M}/Commands/` |

- Query **không** gọi Command & ngược lại.
- Handler **không** gọi handler khác trực tiếp — qua repository/service chung.

---

## Background Jobs ⏳

Dùng cho: gửi notification, escalation alert, cleanup, task chậm không cần kết quả ngay.

**Chưa chốt** (Open Decision — xem `CLAUDE.md`):
- **Hangfire** — persistent, dashboard, retry built-in, cần DB table
- **System.Threading.Channels** — in-process, nhẹ, mất job khi restart

Khi implement: job definition trong `Application/`, worker trong `Api/Backgroundjobs/`. Worker chỉ dispatch MediatR command — **không** chứa business logic.

---

## Caching ⏳

Dùng `IDistributedCache` khi Redis sẵn sàng. **KHÔNG** inject `IMemoryCache` vào Application (vi phạm layer).

Cache-aside pattern (lazy load):

```csharp
var cached = await cache.GetAsync(key, ct);
if (cached is not null) return Deserialize(cached);

var data = await _repo.GetAsync(...);
await cache.SetAsync(key, Serialize(data),
    new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) });
return data;
```

Key format: `beacon:{module}:{entity}:{id}` — xem `nami-conventions/`.

**Invalidation**: xóa cache **ngay sau write thành công** — không dùng TTL làm cơ chế invalidation chính.

---

## Idempotency cho Mobile ⏳

Mobile hay retry khi mạng yếu. Endpoint **tạo resource** (register, checkin, upload) phải hỗ trợ `Idempotency-Key` header:

```
POST /api/v1/checkins
Idempotency-Key: <client-generated-uuid>
```

Store key + response vào Redis (TTL 24h). Key đã tồn tại → trả response cũ, không tạo lại.

---

## Resilience (External Services)

Chưa có retry/circuit breaker. Khi thêm: dùng **Polly**.

Áp dụng retry cho:
- MinIO upload/download (transient network)
- HTTP client gọi ngoài (SMS, notification gateway)

**KHÔNG** retry SQL Server write — EF transaction đã atomic, retry có thể duplicate.

```csharp
services.AddHttpClient<ISmsService, SmsService>()
    .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(3, _ => TimeSpan.FromSeconds(2)));
```

---

## Health Checks

```
GET /health       → Liveness
GET /health/ready → Readiness (SQL Server + MinIO)
GET /health/db, /health/minio
```

Thêm dependency mới (Redis, external API) = **bắt buộc** thêm health check ở `Api/Extensions/HealthCheckExtensions.cs`. Tham chiếu: `Api/HealthChecks/MinioHealthCheck.cs`.

---

## Soft vs Hard Delete

| Trường hợp | Dùng |
|---|---|
| Entity có thể phục hồi / audit | `SoftDeletableEntity` → `IsDeleted = true` |
| File trên MinIO (xóa thật) | Cần permission `media:hard-delete`, chỉ admin |
| Lookup/reference data không nhạy cảm | Hard delete bình thường |

EF query filter trên `IsDeleted` **tự động apply** — truy vấn không cần `Where(x => !x.IsDeleted)`.
