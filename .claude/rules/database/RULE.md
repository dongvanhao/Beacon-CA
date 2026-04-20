# Database — Beacon

> SQL injection → `security/RULE.md`. Repository interface location → `CLAUDE.md`.

---

## Nguyên tắc

| ❌ | ✅ |
|---|---|
| Handler inject `AppDbContext` | Handler inject `IXyzRepository` |
| Generic `IRepository<T>` | Interface domain-meaningful riêng |
| `SELECT *` / load navigation prop không cần | Projection DTO hoặc `Select` |
| Hard delete entity nhạy cảm | `SoftDeletableEntity` → `IsDeleted = true` |

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
- Trả `Entity?` cho single-item query; handler quyết định null = lỗi gì.
- `SaveChangesAsync` gọi trong **handler**, không rải lẻ trong từng repo method.

---

## EF Core Configuration

Mỗi entity = 1 file `IEntityTypeConfiguration<T>` tại `Infrashtructure/Presistence/Configuration/{Module}/`:

```csharp
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("Users");
        b.HasKey(u => u.Id);
        b.Property(u => u.Email).IsRequired().HasMaxLength(256);
        b.HasIndex(u => u.Email).IsUnique();
    }
}
```

- Auto-discover qua `ApplyConfigurationsFromAssembly` — **không** đăng ký thủ công.
- **Soft-delete query filter** đặt trong `AppDbContext.OnModelCreating`, **KHÔNG** trong config class.
- Entity mới → thêm `DbSet<T>` vào `AppDbContext`.

---

## DB Naming

| | Convention | Ví dụ |
|---|---|---|
| Table | PascalCase plural | `Users`, `MediaObjects`, `RefreshTokens` |
| Column | PascalCase (EF default) | `CreatedAtUtc`, `IsDeleted` |
| Index | `IX_{Table}_{Column}` | `IX_Users_Email` |
| FK | `FK_{Table}_{Referenced}_{Column}` | `FK_MediaObjects_Users_UploadedByUserId` |
| PK | `PK_{Table}` | `PK_Users` |

---

## Tránh N+1

```csharp
// ❌ N+1
foreach (var u in await db.Users.ToListAsync()) { await db.Orders.Where(o => o.UserId == u.Id)... }

// ✅ Eager load
await db.Users.Include(u => u.Orders).ToListAsync();

// ✅ Projection (ưu tiên khi chỉ cần ít field)
await db.Users.Select(u => new UserListItem { Id = u.Id, Email = u.Email }).ToListAsync();
```

Verify bằng integration test có SQL log (`EnableSensitiveDataLogging` bật trong Development).

---

## Transactions

Chỉ cần khi nhiều bảng persist atomic trong các `SaveChangesAsync` riêng biệt. Nếu chỉ 1 `SaveChangesAsync` bao tất cả thay đổi → EF tự atomic, **không cần** transaction.

```csharp
using var tx = await db.Database.BeginTransactionAsync(ct);
try
{
    await _userRepo.AddAsync(user, ct);
    await _settingRepo.AddAsync(settings, ct);
    await db.SaveChangesAsync(ct);
    await tx.CommitAsync(ct);
}
catch { await tx.RollbackAsync(ct); throw; }
```

---

## Migrations

```bash
dotnet ef migrations add <Name> --project src/Beacon.Infrashtructure --startup-project src/Beacon.Api
dotnet ef database update       --project src/Beacon.Infrashtructure --startup-project src/Beacon.Api
```

- Migration file **immutable** sau khi apply vào shared env — tạo migration mới, không sửa file cũ.
- Review migration trước `database update`: chú ý `DROP COLUMN` / `DROP TABLE` ngoài ý muốn.
- Seed data trong migration (xem `Init_And_Seed_SuperAdmin`), **không** seed trong `Program.cs`.
