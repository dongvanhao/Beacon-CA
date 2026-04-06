# 09. Hướng dẫn tạo Controller và chuẩn đặt tên Endpoint

## 1. Mục tiêu
Tài liệu này hướng dẫn:
- Cách tạo controller mới theo cấu trúc hiện tại của Beacon-CA.
- Cách dùng các attribute phân quyền (`AdminOnly`, `UserOnly`, `Authenticated`, `PublicApi`).
- Quy tắc đặt tên endpoint theo REST để thống nhất toàn hệ thống.

---

## 2. Thành phần liên quan
- Base controller dùng chung:
  - `src/Beacon.Api/Controllers/BaseController.cs`
- Attribute phân quyền:
  - `src/Beacon.Api/Attributes/AdminOnlyAttribute.cs`
  - `src/Beacon.Api/Attributes/UserOnlyAttribute.cs`
  - `src/Beacon.Api/Attributes/AuthenticatedAttribute.cs`
  - `src/Beacon.Api/Attributes/PublicApiAttribute.cs`
- Current user service:
  - `src/Beacon.Application/Common/Interfaces/IService/ICurrentUserService.cs`
  - `src/Beacon.Infrashtructure/Services/Auth/CurrentUserService.cs`

---

## 3. Template tạo Controller chuẩn
### 3.1 Quy tắc chung
1. Controller kế thừa `BaseController`.
2. Route theo mẫu `api/<resource>` (chữ thường, số nhiều).
3. Controller chỉ điều phối request/response.
4. Nghiệp vụ đặt trong UseCase (Application), không đặt trong controller.
5. Trả response qua `HandleResult(...)` hoặc `CreatedResult(...)`.

### 3.2 Mẫu code
```csharp
using Beacon.Api.Attributes;
using Beacon.Application.Features.Users.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : BaseController
    {
        private readonly GetUserByIdUseCase _getUserByIdUseCase;
        private readonly CreateUserUseCase _createUserUseCase;

        public UsersController(
            GetUserByIdUseCase getUserByIdUseCase,
            CreateUserUseCase createUserUseCase)
        {
            _getUserByIdUseCase = getUserByIdUseCase;
            _createUserUseCase = createUserUseCase;
        }

        [Authenticated]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        {
            var result = await _getUserByIdUseCase.ExecuteAsync(id, cancellationToken);
            return HandleResult(result, "Get user successfully");
        }

        [AdminOnly]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
        {
            var result = await _createUserUseCase.ExecuteAsync(request, cancellationToken);
            return CreatedResult(result, nameof(GetById), new { id = result.Value!.Id }, "Create user successfully");
        }
    }
}
```

---

## 4. Cách dùng Attribute phân quyền
### 4.1 `PublicApi`
- Mục đích: endpoint public, không cần token.
- Dùng cho:
  - health check công khai
  - endpoint metadata/public config

Ví dụ:
```csharp
[PublicApi]
[HttpGet("public-info")]
public IActionResult PublicInfo()
{
    return Ok();
}
```

### 4.2 `Authenticated`
- Mục đích: chỉ cần đăng nhập (admin/user đều được).
- Dùng cho:
  - profile hiện tại
  - đổi mật khẩu
  - update thông tin cá nhân

Ví dụ:
```csharp
[Authenticated]
[HttpGet("me")]
public IActionResult Me()
{
    return Ok();
}
```

### 4.3 `AdminOnly`
- Mục đích: chỉ role `ADMIN`.
- Dùng cho:
  - quản trị người dùng
  - quản lý cấu hình hệ thống
  - báo cáo admin

Ví dụ:
```csharp
[AdminOnly]
[HttpDelete("{id:int}")]
public IActionResult DeleteUser(int id)
{
    return Ok();
}
```

### 4.4 `UserOnly`
- Mục đích: chỉ role `USER`.
- Dùng cho:
  - endpoint dành riêng người dùng cuối

Ví dụ:
```csharp
[UserOnly]
[HttpPost("checkins")]
public IActionResult CreateCheckin()
{
    return Ok();
}
```

---

## 5. Quy tắc đặt tên Endpoint chuẩn nhất (REST)
## 5.1 Quy tắc bắt buộc
1. Dùng danh từ, không dùng động từ trong path.
2. Resource dùng chữ thường, số nhiều.
3. Dùng HTTP method để biểu diễn hành động.
4. Không dùng dấu `_` hoặc PascalCase trong URL.
5. Endpoint con biểu diễn quan hệ cha-con rõ ràng.

Ví dụ đúng:
- `GET /api/users`
- `GET /api/users/10`
- `POST /api/users`
- `PUT /api/users/10`
- `PATCH /api/users/10/status`
- `DELETE /api/users/10`

Ví dụ chưa chuẩn:
- `GET /api/getUsers`
- `POST /api/createUser`
- `GET /api/UserProfile`

## 5.2 Bảng mapping method chuẩn
- `GET /api/resources`: lấy danh sách.
- `GET /api/resources/{id}`: lấy chi tiết.
- `POST /api/resources`: tạo mới.
- `PUT /api/resources/{id}`: cập nhật toàn bộ.
- `PATCH /api/resources/{id}`: cập nhật một phần.
- `DELETE /api/resources/{id}`: xóa.

## 5.3 Quy tắc cho action đặc biệt
Khi nghiệp vụ không thuần CRUD, đặt bằng sub-resource rõ nghĩa:
- `POST /api/auth/login`
- `POST /api/auth/refresh-tokens`
- `POST /api/users/{id}/roles`
- `PATCH /api/users/{id}/password`

Tránh đặt endpoint mơ hồ như:
- `/api/users/do-something`

---

## 6. Quy tắc đặt tên method trong Controller
1. Tên method bắt đầu bằng động từ nghiệp vụ rõ ràng.
2. Có hậu tố `Async` cho method async.
3. Đồng nhất với HTTP method.

Ví dụ:
- `GetByIdAsync`
- `GetListAsync`
- `CreateAsync`
- `UpdateAsync`
- `DeleteAsync`

---

## 7. Mẫu thiết kế route cho các module thường gặp
### 7.1 Users
- `GET /api/users`
- `GET /api/users/{id}`
- `POST /api/users`
- `PATCH /api/users/{id}`
- `DELETE /api/users/{id}`

### 7.2 Admins
- `GET /api/admins`
- `GET /api/admins/{id}`
- `POST /api/admins`

### 7.3 Auth
- `POST /api/auth/login`
- `POST /api/auth/refresh-tokens`
- `POST /api/auth/logout`

### 7.4 Health
- `GET /api/health/database`

---

## 8. Checklist review trước khi merge
1. Controller kế thừa `BaseController`.
2. Endpoint dùng đúng naming chuẩn REST.
3. Đã chọn đúng attribute bảo mật cho từng API.
4. Không chứa business logic trong controller.
5. Response trả qua `HandleResult(...)` hoặc `CreatedResult(...)`.
6. Tất cả API private đều test đủ 3 case:
- Không token -> 401
- Token đúng role -> thành công
- Token sai role -> 403

---

## 9. Ghi chú về Dependency Injection
Dự án hiện tại dùng extension method theo layer:
- `services.AddApplication()`
- `services.AddInfrastructure(configuration)`

Vì vậy khi thêm UseCase/Service/Repository mới theo đúng naming convention, không cần sửa `Program.cs` để `AddScoped` thủ công từng class nữa.
