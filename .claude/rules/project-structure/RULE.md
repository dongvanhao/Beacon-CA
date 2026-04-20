# Project Structure — Beacon

> Chỉ ghi **quy tắc đặt file mới**. Cây thư mục đầy đủ xem trong source.

---

## Dependency Direction (Clean Architecture)

```
Beacon.Api → Beacon.Application → Beacon.Domain ← Beacon.Infrashtructure
                                       ↑
                             Beacon.Shared (cross-cutting)
```

- **Domain**: ZERO framework dep. Không NuGet ngoài BCL.
- **Application**: chỉ Domain + Shared + MediatR/FluentValidation. **Không** import EF Core.
- **Infrastructure**: implement interface từ Domain/Application. **Không** bị Application import.
- **Api**: import Application. Không gọi Infrastructure trực tiếp (chỉ qua DI).

---

## Đặt file mới

### Use case mới (Command/Query)

```
Application/Features/{Module}/
  Commands/{UseCase}/
    {UseCase}Command.cs
    {UseCase}CommandHandler.cs
  Queries/{UseCase}/
    {UseCase}Query.cs
    {UseCase}QueryHandler.cs
  Validators/{Module}/
    {UseCase}CommandValidator.cs     ← validator tách khỏi UC folder để dễ reuse
  Dtos/
    {UseCase}Request.cs
    {Entity}Dto.cs
```

### Entity mới — checklist

1. `Domain/Entities/{Module}/{Entity}.cs` — kế thừa base class đúng
2. `Domain/IRepository/{Module}/I{Entity}Repository.cs`
3. `Infrastructure/Presistence/Configuration/{Module}/{Entity}Configuration.cs`
4. `Infrastructure/Repository/{Module}/{Entity}Repository.cs`
5. Thêm `DbSet<{Entity}>` vào `AppDbContext`
6. Đăng ký repository trong `Infrastructure/Dependencyinjection/InfrastructureServiceExtensions.cs`
7. Tạo migration

### Endpoint mới (controller đã có)

Thêm action method vào controller hiện tại — **không** tạo controller mới cho cùng resource.

### Controller mới

- Thuộc module riêng → `Api/Controllers/{Module}/`
- Top-level resource → `Api/Controllers/`
- Route: `[Route("api/v1/{resource}")]` — **không** `api/[controller]`

### Mapper mới

`Application/Mappings/{Module}/{Entity}{UseCase}Mapper.cs` + đăng ký Singleton trong `ApplicationServiceExtensions.cs`.

### Interface service (cross-layer)

`Application/Common/Interfaces/IService/I{Name}Service.cs` — implement trong `Infrastructure/Services/`.

### Test

```
tests/Beacon.UnitTests/{Module}/{HandlerName}Tests.cs
tests/Beacon.IntergrationTests/{Module}/{Controller}Tests.cs
```

---

## "KHÔNG đặt ở đây"

| Thành phần | ❌ | ✅ |
|---|---|---|
| Business logic | Controller / Repository | Application Handler |
| EF Core query | Handler trực tiếp | Repository |
| DbContext | Application layer | Infrastructure layer |
| Validation logic | Domain Entity | FluentValidation Validator |
| Service registration | `Program.cs` | Extension methods |
| Soft-delete filter | `IEntityTypeConfiguration<T>` | `AppDbContext.OnModelCreating` |
| Error code string | Inline trong handler | `Beacon.Shared/Constants/ErrorCodes.cs` |

---

## Module mới — checklist

Khi thêm module mới (vd `Checkins` → `Billing`):

- [ ] `Domain/Entities/{M}/`, `Domain/IRepository/{M}/`, `Domain/Enums/{M}/`
- [ ] `Application/Features/{M}/` — Commands/Queries/Dtos/Validators
- [ ] `Application/Mappings/{M}/`
- [ ] `Infrastructure/Presistence/Configuration/{M}/`, `Infrastructure/Repository/{M}/`
- [ ] `Api/Controllers/{M}/` (nếu có endpoint)
- [ ] Cập nhật `CLAUDE.md § Domain Modules` 🚧 → ✅
