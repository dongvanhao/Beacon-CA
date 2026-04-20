# Database — Beacon

> SQL injection prevention → `security/RULE.md`. Repository interface location → `CLAUDE.md`.

---

## Quy tắc truy cập dữ liệu

| ❌ | ✅ |
|---|---|
| Handler inject `AppDbContext` trực tiếp | Handler inject `IXyzRepository` |
| Generic `IRepository<T>` | Interface riêng, method có tên domain rõ ràng |
| `SELECT *` / lấy toàn bộ navigation prop không dùng | Project DTO ngay trong query hoặc dùng `Select` |
| Xóa cứng entity nhạy cảm | `SoftDeletableEntity` → `IsDeleted = true` |

---

## Repository Pattern

```csharp
// Domain/IRepository/IUserRepository.cs
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

// Infrastructure/Repository/Identity/UserRepository.cs
public class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct)
        => db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
}
```

- Repository **không** chứa business logic — chỉ query/persist.
- Repository trả `Entity?` (nullable) cho single-item query — handler quyết định null có phải lỗi không.
- `SaveChangesAsync` gọi trong **handler** sau khi hoàn thành thao tác, không gọi trong repository method riêng lẻ (trừ khi repository là unit of work).

---

## Entity Configuration (Fluent API)

Mỗi entity có **một file** `IEntityTypeConfiguration<T>` riêng tại `Infrastructure/Presistence/Configuration/{Module}/`:

```csharp
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        // FK, Index, v.v.
    }
}
```

- Auto-discovered qua `ApplyConfigurationsFromAssembly` — **không cần đăng ký thủ công**.
- **Soft-delete query filter** đặt trong `AppDbContext.OnModelCreating`, **KHÔNG** trong config class.
- Mọi entity mới phải có `DbSet<T>` trong `AppDbContext`.

---

## Naming Convention (DB)

| Thành phần | Convention | Ví dụ |
|---|---|---|
| Table | PascalCase số nhiều | `Users`, `MediaObjects`, `RefreshTokens` |
| Column | PascalCase (EF convention) | `CreatedAtUtc`, `IsDeleted` |
| Index | `IX_[Table]_[Column]` | `IX_Users_Email` |
| Foreign key | `FK_[Table]_[Referenced]_[Column]` | `FK_MediaObjects_Users_UploadedByUserId` |
| Primary key | `PK_[Table]` | `PK_Users` |

---

## Tránh N+1

```csharp
// ❌ N+1
var users = await db.Users.ToListAsync();
foreach (var u in users) { var orders = await db.Orders... }

// ✅ Eager load
var users = await db.Users.Include(u => u.Orders).ToListAsync();

// ✅ Projection (tốt hơn nếu chỉ cần 1 số field)
var dtos = await db.Users
    .Select(u => new UserListItem { Id = u.Id, Email = u.Email })
    .ToListAsync();
```

Verify N+1 bằng integration test có log SQL (sensitive data logging bật trong Development).

---

## Transactions

Dùng khi nhiều bảng phải persist cùng lúc (all-or-nothing):

```csharp
// Trong handler — inject AppDbContext qua IUnitOfWork nếu có, hoặc qua repo riêng
using var tx = await db.Database.BeginTransactionAsync(ct);
try
{
    await _userRepo.AddAsync(user, ct);
    await _settingRepo.AddAsync(settings, ct);
    await db.SaveChangesAsync(ct);
    await tx.CommitAsync(ct);
}
catch
{
    await tx.RollbackAsync(ct);
    throw;
}
```

> Nếu chỉ một `SaveChangesAsync` bao toàn bộ thao tác trong một request → EF tự atomic, không cần transaction tường minh.

---

## Migrations

```bash
# Tạo migration (từ solution root)
dotnet ef migrations add <Name> \
  --project src/Beacon.Infrashtructure \
  --startup-project src/Beacon.Api

# Apply
dotnet ef database update \
  --project src/Beacon.Infrashtructure \
  --startup-project src/Beacon.Api
```

- Migration file là **immutable** sau khi apply vào shared env — không sửa file cũ, tạo migration mới.
- Review file migration trước khi apply: kiểm tra không có `DROP COLUMN` / `DROP TABLE` ngoài ý muốn.
- Seed data đặt trong migration (xem `Init_And_Seed_SuperAdmin`) — không seed trong `Program.cs`.
