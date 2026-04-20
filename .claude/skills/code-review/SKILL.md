---
name: Code Review & Quality
description: Five-axis code review cho Beacon — Clean Architecture, .NET 8
---

# Code Review Skill — Beacon

> "Approve khi code cải thiện codebase, dù chưa hoàn hảo." — Progress over perfection.

---

## Quy trình review

1. **Đọc tests trước** — tests nói lên behavior được expect và edge case nào đã xét
2. **Walk through implementation** theo 5 axes bên dưới
3. **Phân loại findings** → Critical / Important / Nit
4. **Viết feedback actionable** — chỉ rõ file:line + lý do + gợi ý sửa

---

## Five-Axis Checklist

### Axis 1 — Correctness

- [ ] Handler trả `Result.Failure(...)` thay vì `throw` cho lỗi nghiệp vụ?
- [ ] Null check sau `GetByIdAsync`? (`if (entity is null) return Result.Failure(Error.NotFound(...))`)
- [ ] FluentValidation validator tồn tại cho Command/Query mới?
- [ ] `SaveChangesAsync` được gọi sau tất cả write operations trong handler?
- [ ] `CancellationToken` được truyền xuyên suốt async call chain?
- [ ] Edge cases: Guid.Empty, list rỗng, giá trị max/min?

### Axis 2 — Readability

- [ ] Tên class/method/property theo `nami-conventions/RULE.md`?
- [ ] Handler không quá ~50 dòng? (nếu dài hơn → tách thành private method hoặc service)
- [ ] Expression body (`=>`) được dùng cho action 1 dòng trong controller?
- [ ] XML doc block đầy đủ cho endpoint mới? (xem template `api-conventions/RULE.md §8`)

### Axis 3 — Architecture (Critical cho Beacon)

- [ ] Handler không inject `AppDbContext` trực tiếp?
- [ ] Application layer không import EF Core / Infrastructure namespace?
- [ ] Domain entity không chứa business logic phụ thuộc framework?
- [ ] Mapper không có I/O, async, hay business logic?
- [ ] Service mới được đăng ký trong extension method đúng layer (không phải `Program.cs`)?
- [ ] Soft-delete query filter không được đặt trong `IEntityTypeConfiguration<T>`?
- [ ] Entity mới có `DbSet<T>` trong `AppDbContext` và migration chưa?
- [ ] File đặt đúng folder theo `project-structure/RULE.md`?

### Axis 4 — Security

- [ ] Endpoint có `[Authorize]` / `[AdminOnly]` / `[AllowAnonymous]` phù hợp?
- [ ] Resource ownership được check trong **handler** (không phải controller)?
  ```csharp
  if (entity.OwnerId != currentUserId)
      return Result.Failure(Error.Forbidden("...", "..."));
  ```
- [ ] Error message không leak thông tin nhạy cảm (password hash, internal path)?
- [ ] Secret không hardcode trong code hay appsettings?
- [ ] PII không bị log?

### Axis 5 — Performance

- [ ] Query có dùng `Include` cho navigation property thay vì lazy load?
- [ ] List query có pagination (cursor hoặc offset) — không query all rồi filter in-memory?
- [ ] `Select` projection dùng khi không cần toàn bộ entity?
- [ ] Không có `await` trong vòng lặp (N+1 async)?

---

## Comment Labels

| Label | Ý nghĩa | Cần fix trước merge? |
|---|---|---|
| `Critical:` | Bug / security / vi phạm layer boundary | ✅ Bắt buộc |
| (none) | Required change | ✅ Bắt buộc |
| `Nit:` | Style / naming nhỏ | ❌ Tùy tác giả |
| `Optional:` | Gợi ý cải thiện | ❌ Cân nhắc |
| `FYI:` | Thông tin tham khảo | ❌ Không cần action |

```
Critical: Handler inject AppDbContext trực tiếp — vi phạm layer boundary.
          Dùng IUserRepository thay thế.

Nit: Đổi tên `data` → `userProfile` cho rõ hơn.

FYI: MediaDtoMapper đã có extension method WithPresignedUrl() cho pattern này.
```

---

## Output Format

```markdown
## Review: [Tên PR / Feature]

### Tóm tắt
[1-2 câu đánh giá chung]

### Critical (phải fix trước merge)
- **[File:Line]** [Mô tả + lý do + cách sửa]

### Important
- **[File:Line]** [Mô tả]

### Nit / Optional
- **[File:Line]** [Gợi ý]

### Verdict
- [ ] ✅ Approve
- [ ] 🔄 Request changes
- [ ] 💬 Needs discussion
```

---

## Sizing

| Size | Số dòng thay đổi | Đánh giá |
|---|---|---|
| Lý tưởng | ~100 | Review kỹ được |
| Chấp nhận | ~300 | Cần focus |
| Quá lớn | 1000+ | Yêu cầu split |

Mỗi PR = **một slice dọc hoàn chỉnh** (Entity → Handler → Controller) hoặc một refactor cụ thể — không gộp nhiều feature vào một PR.
