---
name: create-endpoint
description: Tạo CRUD endpoint hoàn chỉnh cho một resource mới trong Beacon API
---

# Skill: Tạo endpoint mới

## Input cần có trước khi bắt đầu
- Tên resource và module (VD: resource=BeaconGroup, module=Group)
- Các field của entity và kiểu dữ liệu
- Endpoint nào cần: GET list / GET by id / POST / PUT / DELETE
- Base class: `BaseEntity` / `AuditableEntity` / `SoftDeletableEntity`

## Thứ tự tạo (BẮT BUỘC theo thứ tự này)

### 1. Domain Entity
`src/Beacon.Domain/Entities/{Module}/{Entity}.cs`
- Chọn base: `BaseEntity` (Guid Id) | `AuditableEntity` (+CreatedAtUtc, UpdatedAtUtc) | `SoftDeletableEntity` (+IsDeleted)
- Tất cả property dùng `private set`
- Luôn có `protected {Entity}() { }` cho EF Core
- Thêm factory method hoặc constructor với params bắt buộc
- Namespace: `Beacon.Domain.Entities.{Module}`
  - **Quirk:** module Settings → namespace `Beacon.Domain.Entities.Setting` (không có 's')

### 2. Repository Interface
`src/Beacon.Domain/IRepository/I{Entity}Repository.cs`
- **KHÔNG kế thừa base interface** (IRepository<T> chưa tồn tại)
- Khai báo methods cần thiết: `GetByIdAsync`, `GetAllAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`
- Namespace: `Beacon.Domain.IRepository`

### 3. DTOs
`src/Beacon.Application/Features/{Module}/Dtos/`
- `{Entity}Dto.cs` — response trả về client (KHÔNG expose Entity trực tiếp)
- `Create{Entity}Request.cs` — nhận từ client cho POST
- `Update{Entity}Request.cs` — nhận từ client cho PUT (nếu cần)

### 4. Validator
`src/Beacon.Application/Features/{Module}/Validators/Create{Entity}RequestValidator.cs`
- Kế thừa `AbstractValidator<Create{Entity}Request>`
- Auto-discovered — KHÔNG cần đăng ký thủ công

### 5. Command/Query + Handler
Commands: `src/Beacon.Application/Features/{Module}/Commands/Create{Entity}Command.cs`
Queries: `src/Beacon.Application/Features/{Module}/Queries/Get{Entity}Query.cs`
- Handler nhận `I{Entity}Repository` qua constructor injection
- Handler trả về `Result<T>` hoặc `Result` — KHÔNG throw exception cho business failure
- Dùng `Error.NotFound(...)` / `Error.Conflict(...)` từ `Beacon.Shared`

### 6. EF Core Configuration
`src/Beacon.Infrashtructure/Presistence/Configuration/{Module}/{Entity}Configuration.cs`
- Implement `IEntityTypeConfiguration<{Entity}>`
- Auto-discovered via `ApplyConfigurationsFromAssembly` — KHÔNG cần đăng ký thủ công

### 6.5. Thêm DbSet vào AppDbContext
`src/Beacon.Infrashtructure/Presistence/AppDbContext.cs`

```csharp
public DbSet<{Entity}> {Entity}s => Set<{Entity}>();
```

Nếu entity dùng soft-delete, thêm vào `OnModelCreating`:
```csharp
modelBuilder.Entity<{Entity}>().HasQueryFilter(x => !x.IsDeleted);
```

### 7. Repository Implementation
`src/Beacon.Infrashtructure/Repository/{Module}/{Entity}Repository.cs`
- Implement `I{Entity}Repository`
- Inject `AppDbContext` qua constructor
- Dùng `_context.{Entity}s` để query

### 8. DI + Controller
Đăng ký trong `src/Beacon.Api/Program.cs`:
```csharp
builder.Services.AddScoped<I{Entity}Repository, {Entity}Repository>();
```

Controller: `src/Beacon.Api/Controllers/{Entity}sController.cs`
- Kế thừa `BaseController`
- Inject `IMediator` — KHÔNG inject repository hoặc service trực tiếp
- Route: `[Route("api/v1/[controller]")]`

## Mapping (quan trọng)

**Mapster CHƯA được cài.** Dùng manual property assignment trong Handler:
```csharp
var dto = new {Entity}Dto
{
    Id = entity.Id,
    Name = entity.Name,
    // map từng field thủ công
};
```

## SOLID Checklist (BẮT BUỘC review trước khi commit)

### S — Single Responsibility
- [ ] Mỗi Handler chỉ xử lý đúng 1 command/query — không viết 2 use case trong 1 Handler
- [ ] Handler **chỉ orchestrate**: gọi repository → gọi service → trả Result. KHÔNG chứa business logic phức tạp (tính toán, transformation dài)
- [ ] Nếu Handler dài >50 dòng: tách logic thành domain method trên Entity hoặc service riêng

### O — Open/Closed
- [ ] Thêm endpoint mới = tạo Command/Query + Handler mới, KHÔNG sửa Handler cũ
- [ ] Không hardcode magic string trong Handler (error codes phải dùng `ErrorCodes.{Module}.{CODE}` từ Shared)

### L — Liskov Substitution
- [ ] Repository implementation phải implement đúng toàn bộ interface contract
- [ ] Không throw exception trong method mà interface signature không khai báo throws

### I — Interface Segregation
- [ ] Repository interface chỉ khai báo methods mà Handler thực sự cần
- [ ] Nếu thêm method chỉ dùng cho 1 handler đặc biệt: cân nhắc tách interface hoặc thêm Query method riêng
- [ ] KHÔNG thêm `SaveChangesAsync` vào interface nếu chỉ có 1 implementation

### D — Dependency Inversion
- [ ] Handler inject **interface** (`I{Entity}Repository`), KHÔNG inject concrete class
- [ ] Controller inject `IMediator`, KHÔNG inject repository hoặc service trực tiếp
- [ ] Service/Handler KHÔNG inject `IConfiguration` trực tiếp → dùng `IOptions<T>` nếu cần config
- [ ] `Program.cs` là nơi duy nhất được phép đọc `IConfiguration` trực tiếp (composition root)

## Gotchas (dễ quên)
- Quên đăng ký repository trong `Program.cs` → `InvalidOperationException` lúc runtime
- Quên thêm `DbSet` vào `AppDbContext` → migration không tạo table
- Soft-delete: `HasQueryFilter` phải thêm trong `AppDbContext.OnModelCreating`, KHÔNG phải trong config class (xem `UserDevice`, `EmergencyContact` làm ví dụ)
- Namespace Settings: folder `Settings/` → namespace `Beacon.Domain.Entities.Setting` (không có 's')
- `CurrentUserService` nằm ở `src/Beacon.Api/Services/`, không phải Application layer
- **Sau bước 6.5:** chạy `/add-migration` để tạo migration trước bước 7
