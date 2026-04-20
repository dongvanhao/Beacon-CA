# Naming Conventions — Beacon

> URL/Route → `api-conventions/RULE.md`. DB naming → `database/RULE.md`.

---

## C# Code

| | Convention | Ví dụ |
|---|---|---|
| Class, Interface, Enum, Method, Property | PascalCase | `UserRepository`, `IUserRepository` |
| Local / parameter | camelCase | `userId`, `accessToken` |
| Private field | `_camelCase` | `_userRepo` |
| Constant | PascalCase | `MaxFileSize` |
| Generic type param | `T` / `TEntity` | `Result<T>` |

---

## File/Class theo layer

### Application

| | Pattern | Ví dụ |
|---|---|---|
| Command | `{UseCase}Command` | `LoginCommand`, `UploadMediaCommand` |
| Command Handler | `{UseCase}CommandHandler` | `LoginCommandHandler` |
| Query | `{UseCase}Query` | `GetCurrentUserQuery`, `ListMediaQuery` |
| Query Handler | `{UseCase}QueryHandler` | `GetCurrentUserQueryHandler` |
| Validator | `{CommandOrRequest}Validator` | `LoginRequestValidator` |
| Mapper | `{Entity}{UseCase}Mapper` | `UserAuthMapper`, `MediaDtoMapper` |
| DTO Request | `{UseCase}Request` | `LoginRequest`, `UpdateProfileRequest` |
| DTO Response | `{Entity}Dto` hoặc `{UseCase}Response` | `MediaDto`, `AuthResponse` |
| Interface service | `I{Name}Service` | `IJwtService`, `IStorageService` |

### Domain

| | Pattern | Ví dụ |
|---|---|---|
| Entity | PascalCase singular | `User`, `MediaObject` |
| Repository interface | `I{Entity}Repository` | `IUserRepository` |
| Base entity | `{Capability}Entity` | `AuditableEntity`, `SoftDeletableEntity` |
| Enum | PascalCase singular | `MediaType`, `DevicePlatform` |

### Infrastructure

| | Pattern | Ví dụ |
|---|---|---|
| Repository impl | `{Entity}Repository` | `UserRepository` |
| EF config | `{Entity}Configuration` | `UserConfiguration` |
| Service impl | `{Name}Service` | `JwtService`, `MinioStorageService` |

### API

| | Pattern | Ví dụ |
|---|---|---|
| Controller | `{Resource}Controller` | `AuthController` |
| Authorization attr | `{Name}Attribute` / `{Name}Only` | `HasPermissionAttribute`, `AdminOnlyAttribute` |

---

## Test Naming

```csharp
public class LoginCommandHandlerTests { }
// Method: {Method}_{Scenario}_{ExpectedResult}
public async Task Handle_WithValidCredentials_ReturnsAuthResponse() { }
public async Task Handle_WhenUserNotFound_ReturnsNotFoundError() { }
```

---

## Config & Secrets

`appsettings.json` — **PascalCase** sections, ký tự phân cách `:`. **Chỉ non-secret** — secret (Key/Password/AccessKey) phải ở User Secrets (dev) / Key Vault (prod). Xem `security/RULE.md`.

```json
{ "Jwt": { "Issuer": "beacon", "ExpiryMinutes": 15 },
  "Minio": { "Endpoint": "localhost:9000", "BucketName": "beacon-media" } }
```

Bind strongly-typed:

```csharp
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
```

---

## Cache Keys (khi implement Redis)

Pattern: `{module}:{entity}:{id}` — lowercase.

```
beacon:user:550e8400-...
beacon:rate-limit:auth:ip:192.168.1.1
```

TTL tham khảo: user profile 1h · settings 24h · rate-limit 15 phút.

---

## Namespace theo folder

Match folder path. **Typo đã track — không sửa khi chưa có ADR** (xem `CLAUDE.md § Namespace Quirks`).
