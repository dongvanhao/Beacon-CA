# Tech defaults

## Packages đang dùng (đừng thêm alternatives)
- ORM: Entity Framework Core 8 (KHÔNG dùng Dapper trừ khi query phức tạp)
- Validation: FluentValidation
- Mediator: MediatR
- Mapping: Mapster (KHÔNG dùng AutoMapper)
- Auth: ASP.NET Core Identity + JWT Bearer
- Logging: Serilog (đã config trong Program.cs)
- Caching: IMemoryCache (chưa có Redis)

## Response format chuẩn
{
  "success": true,
  "data": { ... },
  "message": null,
  "errors": []
}
Class: ApiResponse ở Beacon.Application/Common/

## Error handling
- Dùng GlobalExceptionMiddleware (đã có) — KHÔNG try/catch toàn bộ
- Custom exceptions: NotFoundException, ConflictException, ValidationException
- Middleware tự map exception → HTTP status

## SignalR
Hub tại: Beacon.API/Hubs/BeaconHub.cs
FE connect tại: /hubs/beacon
Dùng để push real-time khi trạng thái beacon thay đổi