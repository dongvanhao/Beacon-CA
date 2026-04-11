# Ví dụ output review tốt

[BeaconsController.cs:23] Inject IBeaconService trực tiếp
→ Đổi sang IMediator: private readonly IMediator _mediator;

[BeaconsController.cs:45] POST trả 200 OK
→ Đổi thành 201 Created + trả về location header

[CreateBeaconHandler.cs:67] Không có CancellationToken
→ Thêm param: Handle(CreateBeaconCommand cmd, CancellationToken ct)

Điểm: 6/10
Ưu tiên: (1) Chuyển sang MediatR pattern, (2) Sửa HTTP status codes