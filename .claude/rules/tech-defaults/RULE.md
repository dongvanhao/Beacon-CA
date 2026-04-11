# Tech defaults

## Packages đang dùng (đừng thêm alternatives)
- ORM: Entity Framework Core 8 (KHÔNG dùng Dapper trừ khi query phức tạp)
- Validation: FluentValidation 12.1.1
- Mediator: MediatR 14.1.0
- Mapping: Mapster — **CHƯA INSTALL**. Dùng manual property assignment cho đến khi cài.
- Auth: Custom JWT Bearer — **KHÔNG dùng ASP.NET Core Identity**. `User` extends `AuditableEntity`, KHÔNG phải `IdentityUser`. Password hash thủ công.
- Logging: Serilog (đã config trong Program.cs)
- Caching: IMemoryCache (chưa có Redis)

## Response format chuẩn

```json
{
  "success": true,
  "message": "...",
  "code": null,
  "data": {},
  "errors": null
}
```

Class: `ApiResponse<T>` tại `src/Beacon.Shared/Common/Responses/ApiResponse.cs`
Namespace: `Beacon.Shared.Common.Responses`

## Error handling
- Dùng `ExceptionHandlingMiddleware` (đã có) — KHÔNG try/catch toàn bộ
- Custom exceptions: `NotFoundException`, `ConflictException`, `ValidationException`, `UnauthorizedException`, `ForbiddenException`
- Middleware tự map exception → HTTP status
- Business failure: dùng `Result.Failure(Error.NotFound(...))` — KHÔNG throw exception

## SignalR
SignalR **CHƯA implement**. `BeaconHub.cs` chưa tồn tại — đừng reference file này.
Kế hoạch: Hub tại `/hubs/beacon` để push real-time khi trạng thái beacon thay đổi.
