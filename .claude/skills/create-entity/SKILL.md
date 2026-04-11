---
name: create-entity
description: Tạo Domain Entity và EF Core configuration (không tạo full CRUD endpoint)
---

# Skill: Tạo Domain Entity

Dùng skill này khi cần scaffold entity mới mà chưa cần full CRUD endpoint.
Cho full CRUD, dùng `/create-endpoint` thay thế.

## Bước 1: Chọn base class

| Base Class | Có gì | Dùng khi |
|---|---|---|
| `BaseEntity` | `Guid Id` | Entity đơn giản, không cần audit |
| `AuditableEntity` | + `CreatedAtUtc`, `UpdatedAtUtc` | Cần biết khi nào tạo/cập nhật |
| `SoftDeletableEntity` | + `IsDeleted`, `DeletedAtUtc`, `Delete()`, `Restore()` | Cần xóa mềm |

## Bước 2: Tạo entity file

`src/Beacon.Domain/Entities/{Module}/{Entity}.cs`

```csharp
using Beacon.Domain.Common;

namespace Beacon.Domain.Entities.{Module};

public class {Entity} : AuditableEntity  // hoặc BaseEntity, SoftDeletableEntity
{
    public string {Property} { get; private set; } = default!;
    public Guid {ForeignKey}Id { get; private set; }

    // Navigation property
    public {RelatedEntity}? {RelatedEntity} { get; private set; }

    // Required: EF Core cần constructor không tham số
    protected {Entity}() { }

    // Factory method (preferred)
    public static {Entity} Create(string property, Guid foreignKeyId)
    {
        return new {Entity}
        {
            {Property} = property,
            {ForeignKey}Id = foreignKeyId
        };
    }
}
```

**Namespace quirk:** folder `Settings/` → namespace `Beacon.Domain.Entities.Setting` (không có 's')

## Bước 3: Tạo EF Configuration

`src/Beacon.Infrashtructure/Presistence/Configuration/{Module}/{Entity}Configuration.cs`

```csharp
using Beacon.Domain.Entities.{Module};
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrashtructure.Presistence.Configuration.{Module};

public class {Entity}Configuration : IEntityTypeConfiguration<{Entity}>
{
    public void Configure(EntityTypeBuilder<{Entity}> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.{Property})
            .IsRequired()
            .HasMaxLength(200);

        // Relationships
        builder.HasOne(x => x.{RelatedEntity})
            .WithMany(x => x.{Entity}s)
            .HasForeignKey(x => x.{ForeignKey}Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

Auto-discovered via `ApplyConfigurationsFromAssembly` — KHÔNG cần đăng ký thủ công.

## Bước 4: Thêm DbSet vào AppDbContext

`src/Beacon.Infrashtructure/Presistence/AppDbContext.cs`

```csharp
public DbSet<{Entity}> {Entity}s => Set<{Entity}>();
```

## Bước 5: Soft-delete query filter (nếu dùng SoftDeletableEntity)

Trong `AppDbContext.OnModelCreating` — **KHÔNG phải trong config class**:

```csharp
modelBuilder.Entity<{Entity}>().HasQueryFilter(x => !x.IsDeleted);
```

Xem `UserDevice`, `EmergencyContact`, `MediaObject` làm ví dụ.

## Bước 6: Tạo migration

Chạy `/add-migration` skill với tên migration phù hợp (VD: `Add{Entity}Table`).
