# Project Structure — Beacon

> Cây thư mục đầy đủ → `CLAUDE.md § Project Structure`. Rule này chỉ ghi **quy tắc đặt file mới**.

---

## Dependency Direction (Clean Architecture)

```
Beacon.Api → Beacon.Application → Beacon.Domain ← Beacon.Infrashtructure
                                         ↑
                               Beacon.Shared (cross-cutting)
```

- **Domain**: ZERO dependency framework. Không được import bất kỳ NuGet nào ngoài BCL.
- **Application**: phụ thuộc Domain + Shared + MediatR/FluentValidation. Không được import EF Core.
- **Infrastructure**: implement interface từ Domain/Application. Không được bị import bởi Application.
- **Api**: import Application. Không gọi Infrastructure trực tiếp (chỉ qua DI).

---

## Đặt file mới ở đâu?

### Thêm use case mới (Command/Query)

```
Application/Features/{Module}/
  Commands/{UseCase}/
    {UseCase}Command.cs
    {UseCase}CommandHandler.cs
  Queries/{UseCase}/
    {UseCase}Query.cs
    {UseCase}QueryHandler.cs
  Validators/{Module}/
    {UseCase}CommandValidator.cs   ← tách khỏi use case folder
  Dtos/
    {UseCase}Request.cs
    {Entity}Dto.cs
```

### Thêm entity mới

1. `Domain/Entities/{Module}/{Entity}.cs` — kế thừa đúng base class
2. `Domain/IRepository/{Module}/I{Entity}Repository.cs`
3. `Infrastructure/Presistence/Configuration/{Module}/{Entity}Configuration.cs`
4. `Infrastructure/Repository/{Module}/{Entity}Repository.cs`
5. Thêm `DbSet<{Entity}>` vào `AppDbContext`
6. Đăng ký repository trong `Infrastructure/Dependencyinjection/InfrastructureServiceExtensions.cs`
7. Tạo migration

### Thêm endpoint mới (controller đã có)

Chỉ cần thêm action method vào controller hiện tại — không tạo controller mới cho cùng resource.

### Thêm controller mới

- Đặt trong `Api/Controllers/{Module}/` nếu thuộc module riêng
- Đặt trong `Api/Controllers/` nếu là top-level resource
- Route: `[Route("api/v1/{resource}")]` — không dùng `api/[controller]`

### Thêm mapper mới

`Application/Mappings/{Module}/{Entity}{UseCase}Mapper.cs` — đăng ký Singleton trong `ApplicationServiceExtensions.cs`.

### Thêm interface service (cross-layer)

`Application/Common/Interfaces/IService/I{Name}Service.cs` — implement trong `Infrastructure/Services/`.

### Thêm test

```
tests/Beacon.UnitTests/{Module}/
  {HandlerName}Tests.cs

tests/Beacon.IntergrationTests/{Module}/
  {Controller}Tests.cs
```

---

## Quy tắc "KHÔNG đặt ở đây"

| Thành phần | ❌ Đặt nhầm | ✅ Đúng chỗ |
|---|---|---|
| Business logic | Controller / Repository | Application Handler |
| EF Core query | Handler trực tiếp | Repository |
| DbContext | Application layer | Infrastructure layer |
| Validation logic | Domain Entity | FluentValidation Validator |
| Service registration | `Program.cs` | Extension methods (Infrastructure/Application/Api) |
| Soft-delete query filter | `IEntityTypeConfiguration<T>` | `AppDbContext.OnModelCreating` |
| Error code string | Handler inline | `Beacon.Shared/Constants/ErrorCodes.cs` |

---

## Module mới — checklist

Khi thêm module hoàn toàn mới (ví dụ `Checkins` → `Billing`):

- [ ] `Domain/Entities/{Module}/` — entities
- [ ] `Domain/IRepository/{Module}/` — interfaces
- [ ] `Domain/Enums/{Module}/` — enums nếu có
- [ ] `Application/Features/{Module}/` — Commands, Queries, Dtos, Validators
- [ ] `Application/Mappings/{Module}/` — mappers
- [ ] `Infrastructure/Presistence/Configuration/{Module}/` — EF configs
- [ ] `Infrastructure/Repository/{Module}/` — implementations
- [ ] `Api/Controllers/{Module}/` — controller (nếu có endpoint)
- [ ] Cập nhật `CLAUDE.md § Domain Modules` — đổi status từ 🚧 → ✅
