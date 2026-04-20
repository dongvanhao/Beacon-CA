# Naming Conventions — Beacon

> URL/Route naming → `api-conventions/RULE.md`. DB table/column/index naming → `database/RULE.md`.

---

## C# Code

| Thành phần | Convention | Ví dụ |
|---|---|---|
| Class, Interface, Enum, Property, Method | PascalCase | `UserRepository`, `IUserRepository`, `GetByIdAsync` |
| Local variable, parameter | camelCase | `userId`, `accessToken` |
| Private field | `_camelCase` | `_userRepo`, `_jwtService` |
| Constant | PascalCase (C# convention) | `MaxFileSize`, `DefaultPageSize` |
| Enum member | PascalCase | `ErrorType.NotFound`, `MediaType.Image` |
| Generic type param | `T` hoặc `TEntity` | `Result<T>`, `IRepository<TEntity>` |

---

## File & Class Naming — theo layer

### Application Layer

| Loại | Pattern | Ví dụ |
|---|---|---|
| Command | `{UseCase}Command` | `LoginCommand`, `UploadMediaCommand` |
| Command Handler | `{UseCase}CommandHandler` | `LoginCommandHandler` |
| Query | `{UseCase}Query` | `GetCurrentUserQuery`, `ListMediaQuery` |
| Query Handler | `{UseCase}QueryHandler` | `GetCurrentUserQueryHandler` |
| Validator | `{CommandOrQuery}Validator` | `LoginRequestValidator`, `UploadMediaCommandValidator` |
| Mapper | `{Entity}{UseCase}Mapper` | `UserAuthMapper`, `MediaDtoMapper` |
| DTO Request | `{UseCase}Request` | `LoginRequest`, `UpdateProfileRequest` |
| DTO Response | `{Entity}Dto` hoặc `{UseCase}Response` | `MediaDto`, `AuthResponse`, `UserProfileDto` |
| Interface service | `I{Name}Service` | `IJwtService`, `IStorageService` |

### Domain Layer

| Loại | Pattern | Ví dụ |
|---|---|---|
| Entity | PascalCase singular | `User`, `MediaObject`, `RefreshToken` |
| Repository interface | `I{Entity}Repository` | `IUserRepository`, `IMediaObjectRepository` |
| Base entity | `{Capability}Entity` | `BaseEntity`, `AuditableEntity`, `SoftDeletableEntity` |
| Enum | PascalCase singular | `MediaType`, `ErrorType`, `DevicePlatform` |

### Infrastructure Layer

| Loại | Pattern | Ví dụ |
|---|---|---|
| Repository impl | `{Entity}Repository` | `UserRepository`, `MediaObjectRepository` |
| EF config | `{Entity}Configuration` | `UserConfiguration`, `MediaObjectConfiguration` |
| Service impl | `{Name}Service` | `JwtService`, `MinioStorageService` |

### API Layer

| Loại | Pattern | Ví dụ |
|---|---|---|
| Controller | `{Resource}Controller` | `AuthController`, `MediaController` |
| Authorization attribute | `{Name}Attribute` hoặc `{Name}Only` | `HasPermissionAttribute`, `AdminOnlyAttribute` |

---

## Test Naming

```csharp
// Class: {HandlerOrService}Tests
public class LoginCommandHandlerTests { }

// Method: {Method}_{Scenario}_{ExpectedResult}
public async Task Handle_WithValidCredentials_ReturnsAuthResponse() { }
public async Task Handle_WithInvalidPassword_ReturnsUnauthorizedError() { }
public async Task Handle_WhenUserNotFound_ReturnsNotFoundError() { }
```

---

## Configuration & Secrets (appsettings)

.NET dùng **PascalCase** cho section/key, phân cách bằng `:`:

```json
// appsettings.json — chỉ non-secret
{
  "Jwt": { "Issuer": "beacon", "Audience": "beacon-client", "ExpiryMinutes": 15 },
  "Minio": { "Endpoint": "localhost:9000", "BucketName": "beacon-media" },
  "ConnectionStrings": { "DefaultConnection": "..." }
}
```

Secret (Key, Password, AccessKey) → User Secrets (dev) / Key Vault (prod). Xem `security/RULE.md`.

Bind qua `IConfiguration["Section:Key"]` hoặc strongly-typed options:

```csharp
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
```

---

## Cache Keys (chuẩn bị cho khi implement Redis)

Pattern: `{module}:{entity}:{id}` — tất cả lowercase:

```
beacon:user:550e8400-e29b-41d4-a716-446655440000
beacon:media:550e8400-e29b-41d4-a716-446655440000
beacon:rate-limit:auth:ip:192.168.1.1
```

TTL tham khảo: user profile = 1h, settings = 24h, rate-limit window = 15 phút.

---

## Namespace — theo folder

Namespace match theo đúng folder path. Chú ý các typo đã track:

| Folder | Namespace thực tế |
|---|---|
| `Beacon.Infrashtructure/...` | `Beacon.Infrashtructure` (typo — không sửa cho đến khi có ADR) |
| `Infrashtructure/Dependencyinjection/` | `Beacon.Infrashtructure.Dependencyinjection` (lowercase `i`) |
| `Domain/Entities/Settings/` | `Beacon.Domain.Entities.Setting` (không có `s`) |
