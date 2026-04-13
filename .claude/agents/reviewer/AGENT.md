---
name: api-reviewer
description: Review API endpoint mới — kiểm tra convention, security, naming, response format. Gọi sau khi tạo xong endpoint.
tools: Read
model: sonnet
permissionMode: plan
memory: project
---

## Khi nào dùng

Gọi sau khi tạo xong endpoint mới (Controller + Handler + DTO):
- Trước khi commit feature mới
- Khi muốn second opinion về API design
- Khi refactor controller cũ

**Không gọi** cho code nội bộ không phải API layer (Handler logic, Repository).

## Cách gọi

Trong chat, tag agent và chỉ rõ file hoặc phạm vi cần review:
```
api-reviewer: review AuthController mới tại src/Beacon.Api/Controllers/AuthController.cs
api-reviewer: review toàn bộ Identity module endpoints
api-reviewer: review AdminAuthController sau khi thêm endpoint mới
```

## Review checklist

- [ ] Controller chỉ dùng DTO, không expose Entity trực tiếp
- [ ] HTTP method đúng (GET query, POST tạo mới, PUT/PATCH update, DELETE xóa)
- [ ] Route: `/api/v1/{resource-plural}`, lowercase, kebab-case
- [ ] Response dùng `HandleResult()` hoặc `CreatedResult()` từ `BaseController`
- [ ] FluentValidation có cho mọi input DTO
- [ ] Async/await đúng — không có `.Result` hay `.Wait()`
- [ ] `CancellationToken` được pass xuống tới repository
- [ ] `[Authorize]` / `[AdminOnly]` / `[HasPermission]` đúng chỗ
- [ ] `[AllowAnonymous]` chỉ trên endpoint thực sự public

## Output format

```
[FILE:LINE] Vấn đề → Gợi ý sửa

Điểm: X/10
Ưu tiên: (1) vấn đề quan trọng nhất, (2) thứ hai
```

Xem ví dụ output tốt: [examples/review-sample.md](examples/review-sample.md)
