---
name: security-review
description: Security audit cho Beacon — .NET 8, Clean Architecture, JWT, EF Core
---

# Security Review Skill — Beacon

> Quy tắc chi tiết → `security/RULE.md`. Skill này là **checklist chạy khi audit**.

---

## 🔴 Critical — Kiểm tra đầu tiên

### Secrets không được hardcode

```powershell
# Tìm secret trong source code
Select-String -Path "src/**/*.cs","src/**/*.json" -Pattern '(?i)(password|secret|apikey|connectionstring)\s*=\s*"[^"]' -Recurse

# Kiểm tra appsettings.json có chứa Jwt:Key hoặc password không
Select-String -Path "src/**/appsettings*.json" -Pattern '(?i)(key|password|secret)\s*:\s*"[^"]' -Recurse
```

- [ ] `appsettings.json` / `appsettings.Development.json` không chứa `Jwt:Key`, DB password, MinIO secret
- [ ] Các file trên **không bị commit** trong git history: `git log --all -- "**appsettings*.json"`

### SQL Injection (EF Core)

```powershell
# Tìm FromSqlRaw với string interpolation — dễ bị injection
Select-String -Path "src/**/*.cs" -Pattern 'FromSqlRaw\s*\(\s*\$"' -Recurse
Select-String -Path "src/**/*.cs" -Pattern 'FromSqlRaw\s*\(\s*".*\{' -Recurse
```

- [ ] Không có `FromSqlRaw($"...{userInput}...")` — dùng parameterized hoặc LINQ

### Authorization trên mọi controller

```powershell
# Controller không có [Authorize] / [AllowAnonymous] / [AdminOnly]
Select-String -Path "src/Beacon.Api/Controllers/**/*.cs" -Pattern 'class \w+Controller' -Recurse
```

- [ ] Mọi controller có `[Authorize]` ở class level **hoặc** mọi action có attribute riêng
- [ ] Không có action nào vô tình public do thiếu attribute

---

## 🟡 High Priority

### Owner check

- [ ] Mọi endpoint trả data của một user cụ thể: handler có kiểm tra `entity.OwnerId == currentUserId`?
- [ ] Tìm handler query theo `id` mà không có ownership check:
  ```powershell
  Select-String -Path "src/Beacon.Application/**/*Handler.cs" -Pattern 'GetByIdAsync' -Recurse
  # Review thủ công từng kết quả — có check userId không?
  ```

### Admin endpoints

- [ ] Mọi action xóa/sửa dữ liệu admin có `[AdminOnly]`?
- [ ] Action cần permission cụ thể có `[HasPermission("resource:action")]`?
  ```powershell
  Select-String -Path "src/Beacon.Api/Controllers/**/*.cs" -Pattern '\[HttpDelete\]|\[HttpPut\]' -Recurse
  # Verify mỗi kết quả có [Authorize] / [AdminOnly] tương ứng
  ```

### Validation coverage

```powershell
# Command/Query không có Validator
$handlers = Get-ChildItem "src/Beacon.Application/Features" -Filter "*CommandHandler.cs" -Recurse
$validators = Get-ChildItem "src/Beacon.Application/Features" -Filter "*Validator.cs" -Recurse
# So sánh danh sách — mỗi Command phải có Validator tương ứng
```

- [ ] Mọi `Command` có input từ client đều có `AbstractValidator<TCommand>`?

### PII trong logs

```powershell
Select-String -Path "src/**/*.cs" -Pattern '(logger|_logger|Log)\.\w+\(.*\b(password|email|phone|token)\b' -Recurse
```

- [ ] Không log password, email, phone, token, refreshToken

### Soft delete

```powershell
# Tìm hard delete trên entity nhạy cảm
Select-String -Path "src/**/*.cs" -Pattern '_\w+(repo|Repo|Repository)\.Delete\(' -Recurse
```

- [ ] `User`, `RefreshToken`, `MediaObject` không bị hard delete ngoài flow được phép

---

## 🟢 Medium Priority

### CORS

- [ ] `AuthExtensions.cs` không dùng `AllowAnyOrigin()` — chỉ whitelist origin cụ thể
  ```powershell
  Select-String -Path "src/Beacon.Api/Extensions/AuthExtensions.cs" -Pattern 'AllowAnyOrigin'
  ```

### Dependency vulnerabilities

```powershell
dotnet list package --vulnerable --include-transitive
```

- [ ] Không có package với mức `Critical` hoặc `High`

### Error response leak

```powershell
Select-String -Path "src/**/*.cs" -Pattern 'ex\.Message|ex\.StackTrace|ex\.ToString\(\)' -Recurse
```

- [ ] Exception detail không được trả thẳng về client — `ExceptionHandlingMiddleware` phải handle

### JWT configuration

- [ ] `Jwt:ExpiryMinutes` ≤ 15, `Jwt:RefreshExpiryDays` ≤ 7
- [ ] Key length ≥ 32 chars (256-bit) — verify bằng đo độ dài trong User Secrets

---

## Output Format

```markdown
# Security Audit — Beacon — [Date]

## Critical
- **[File:Line]** [Mô tả + tác động + cách fix]

## High
- **[File:Line]** [Mô tả]

## Medium / Informational
- **[File:Line]** [Mô tả]

## Tóm tắt
- Critical: N issues
- High: N issues
- Recommended action: [...]
```
