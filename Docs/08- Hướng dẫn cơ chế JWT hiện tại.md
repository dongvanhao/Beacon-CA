# 08. Hướng dẫn cơ chế JWT hiện tại

## 1. Mục tiêu
Tài liệu này giải thích:
- JWT hiện tại hoạt động như thế nào trong Beacon-CA
- Cách hệ thống xác thực token Bearer
- Cách lấy thông tin user hiện tại qua `ICurrentUserService`
- Cách dùng các attribute phân quyền cho API

---

## 2. Các thành phần chính
### 2.1 Cấu hình và middleware
- Cấu hình JWT: `src/Beacon.Api/appsettings.json` (section `Jwt`)
- Đăng ký Bearer authentication: `src/Beacon.Api/Program.cs`
- Pipeline xử lý:
  - `app.UseAuthentication()`
  - `app.UseAuthorization()`

### 2.2 Service tạo token
- Interface: `src/Beacon.Application/Common/Interfaces/IService/IJwtService.cs`
- Implementation: `src/Beacon.Infrashtructure/Services/Auth/JwtService.cs`

`JwtService` hiện tại cung cấp:
- `GenerateAccessTokenForAdmin(Admin admin)`
- `GenerateAccessTokenForUser(User user)`
- `GenerateRefreshToken()`

### 2.3 Service đọc thông tin user hiện tại
- Interface: `src/Beacon.Application/Common/Interfaces/IService/ICurrentUserService.cs`
- Model: `src/Beacon.Application/Common/Models/CurrentUserInfo.cs`
- Implementation: `src/Beacon.Infrashtructure/Services/Auth/CurrentUserService.cs`

### 2.4 Attribute phân quyền API
- `AdminOnlyAttribute`: chỉ role `ADMIN`
- `UserOnlyAttribute`: chỉ role `USER`
- `AuthenticatedAttribute`: chỉ cần đăng nhập (không giới hạn role)
- `PublicApiAttribute`: cho phép gọi không cần đăng nhập

Files:
- `src/Beacon.Api/Attributes/AdminOnlyAttribute.cs`
- `src/Beacon.Api/Attributes/UserOnlyAttribute.cs`
- `src/Beacon.Api/Attributes/AuthenticatedAttribute.cs`
- `src/Beacon.Api/Attributes/PublicApiAttribute.cs`

Role constants dùng chung:
- `src/Beacon.Shared/Common/SystemRoles.cs`

---

## 3. Luồng hoạt động JWT hiện tại
### Bước 1: API phát token
Một use case đăng nhập (admin/user) gọi `IJwtService` để tạo access token.

Trong access token hiện có các claim chính:
- `sub`: id user/admin
- `unique_name`: username
- `nameidentifier`: id user/admin
- `name`: username
- `role`: `ADMIN` hoặc `USER`

### Bước 2: Client gửi token
Client gọi API kèm header:

```http
Authorization: Bearer <access_token>
```

### Bước 3: ASP.NET Core xác thực token
`AddAuthentication().AddJwtBearer(...)` trong `Program.cs` sẽ:
- Validate issuer
- Validate audience
- Validate signing key
- Validate lifetime (hết hạn)

Nếu hợp lệ, ASP.NET tạo `HttpContext.User` chứa claims.

### Bước 4: Authorization quyết định quyền truy cập
Dựa trên attribute của endpoint:
- `AdminOnlyAttribute` -> yêu cầu role `ADMIN`
- `UserOnlyAttribute` -> yêu cầu role `USER`
- `AuthenticatedAttribute` -> chỉ cần token hợp lệ
- `PublicApiAttribute` -> bỏ qua auth

### Bước 5: Lấy thông tin user hiện tại trong use case/service
Code nghiệp vụ không đọc trực tiếp `HttpContext`.
Thay vào đó inject `ICurrentUserService` và gọi:
- `GetCurrentUser()`
- `GetUserId()`
- `GetRole()`
- `IsAuthenticated()`

Cách này giữ đúng hướng clean architecture (Application phụ thuộc abstraction).

---

## 4. Cách sử dụng trong Controller
Ví dụ endpoint chỉ admin:

```csharp
[AdminOnly]
[HttpGet("admin-only")]
public IActionResult GetForAdmin()
{
    return Ok();
}
```

Ví dụ endpoint chỉ user:

```csharp
[UserOnly]
[HttpGet("user-only")]
public IActionResult GetForUser()
{
    return Ok();
}
```

Ví dụ endpoint yêu cầu đăng nhập:

```csharp
[Authenticated]
[HttpGet("me")]
public IActionResult Me()
{
    return Ok();
}
```

Ví dụ endpoint public:

```csharp
[PublicApi]
[HttpGet("public")]
public IActionResult Public()
{
    return Ok();
}
```

---

## 5. Cách sử dụng `ICurrentUserService`
Ví dụ trong use case/application service:

```csharp
public class GetProfileUseCase
{
    private readonly ICurrentUserService _currentUserService;

    public GetProfileUseCase(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public void Execute()
    {
        var current = _currentUserService.GetCurrentUser();

        if (!current.IsAuthenticated)
        {
            throw new UnauthorizedException("Unauthorized");
        }

        // current.UserId, current.UserName, current.Role
    }
}
```

---

## 6. Cấu hình JWT cần có
Trong `src/Beacon.Api/appsettings.json`:

```json
"Jwt": {
  "Issuer": "Beacon.Api",
  "Audience": "Beacon.Client",
  "SecretKey": "CHANGE_ME_TO_AT_LEAST_32_CHARACTERS_SECRET_KEY",
  "AccessTokenExpirationMinutes": 30,
  "RefreshTokenExpirationDays": 30
}
```

Lưu ý:
1. `SecretKey` tối thiểu 32 ký tự.
2. Không dùng secret hard-code cho production.
3. Nên dùng biến môi trường/secret manager theo môi trường triển khai.

---

## 7. Lỗi thường gặp
### 7.1 401 Unauthorized
Nguyên nhân thường gặp:
- Thiếu header `Authorization`
- Token hết hạn
- Sai issuer/audience/secret

### 7.2 403 Forbidden
Nguyên nhân:
- Đã đăng nhập nhưng role không đủ (ví dụ user gọi API admin)

### 7.3 Có token nhưng không đọc được user id
Nguyên nhân:
- Token không có claim `nameidentifier` hoặc `sub`
- Claim value không parse được sang `int`

---

## 8. Checklist khi thêm API mới
1. Xác định endpoint cần public hay cần auth.
2. Chọn attribute phù hợp (`PublicApi`, `Authenticated`, `AdminOnly`, `UserOnly`).
3. Nếu cần thông tin user hiện tại, inject `ICurrentUserService`.
4. Không đọc trực tiếp claims ở nhiều nơi, ưu tiên qua `ICurrentUserService`.
5. Test đủ 4 tình huống:
- Không token
- Token hợp lệ đúng role
- Token hợp lệ sai role
- Token hết hạn/invalid
