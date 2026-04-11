---
name: create-endpoint
description: Tạo CRUD endpoint hoàn chỉnh cho một resource mới trong Beacon API
---

# Skill: Tạo endpoint mới

## Input cần có trước khi bắt đầu
- Tên resource (VD: BeaconGroup)
- Các field của entity
- Endpoint nào cần: GET list / GET by id / POST / PUT / DELETE

## Thứ tự tạo (BẮT BUỘC theo thứ tự này)

### 1. Domain Entity
src/Beacon.Domain/Entities/{ResourceName}.cs
- Kế thừa BaseEntity (có Id, CreatedAt, UpdatedAt)
- Private setters, dùng constructor hoặc factory method

### 2. Repository Interface
src/Beacon.Application/Interfaces/I{ResourceName}Repository.cs
- Kế thừa IRepository
- Thêm methods đặc thù nếu cần

### 3. DTOs
src/Beacon.Application/DTOs/{ResourceName}/
- {ResourceName}Response.cs — trả về client
- Create{ResourceName}Request.cs — nhận từ client
- Update{ResourceName}Request.cs — nhận từ client

### 4. Validator
src/Beacon.Application/Validators/Create{ResourceName}RequestValidator.cs

### 5. MediatR Command/Query + Handler
src/Beacon.Application/Features/{ResourceName}/Commands/
src/Beacon.Application/Features/{ResourceName}/Queries/

### 6. EF Repository Implementation
src/Beacon.Infrastructure/Repositories/{ResourceName}Repository.cs

### 7. Register DI
src/Beacon.API/Extensions/ServiceExtensions.cs
— Thêm: services.AddScoped();

### 8. Controller
src/Beacon.API/Controllers/{ResourceName}sController.cs
- [ApiController], [Route("api/[controller]")]
- Inject IMediator — không inject service trực tiếp

## Gotchas (dễ quên)
- Quên register repository trong DI → NullReferenceException lúc runtime
- Quên thêm DbSet vào BeaconDbContext → migration sẽ không tạo table
- Mapping: kiểm tra MappingProfile.cs, thêm mapping mới nếu cần
- Sau bước 6: gọi ef-migration-agent để tạo migration