---
name: setup-di
description: Hướng dẫn đăng ký Dependency Injection cho services và repositories trong Beacon API
---

# Skill: Setup Dependency Injection

## Trạng thái hiện tại

DI đang được đăng ký **inline trong `Program.cs`** (`src/Beacon.Api/Program.cs`).

3 folder DI extension tồn tại nhưng chưa có file:
- `src/Beacon.Application/DependencyInjection/`
- `src/Beacon.Infrashtructure/Dependencyinjection/` (chú ý chữ 'i' thường)
- `src/Beacon.Api/Extensions/`

## Đăng ký service mới (cách hiện tại)

Thêm vào `Program.cs`, trước `var app = builder.Build()`:

```csharp
// Repository
builder.Services.AddScoped<I{Resource}Repository, {Resource}Repository>();

// Service khác
builder.Services.AddScoped<I{ServiceName}, {ServiceName}>();
```

## Lifetime rules

| Lifetime | Dùng khi |
|---|---|
| `AddScoped` | Repository, Service liên quan đến HTTP request (90% trường hợp) |
| `AddSingleton` | Cache, config object, background service stateless |
| `AddTransient` | Helper stateless không giữ state, rất ít dùng |

## SOLID — DIP: Cấu hình typed với IOptions<T>

**KHÔNG** inject `IConfiguration` vào Service/Repository (vi phạm DIP). Thay vào đó dùng pattern `IOptions<T>`:

```csharp
// 1. Tạo typed settings class (trong Infrastructure hoặc Application)
public class MyServiceSettings
{
    public string ApiKey { get; set; } = default!;
    public int TimeoutSeconds { get; set; } = 30;
}

// 2. Đăng ký trong Program.cs (composition root — được phép đọc IConfiguration ở đây)
builder.Services.Configure<MyServiceSettings>(
    builder.Configuration.GetSection("MyServiceSettings"));

// 3. Service inject IOptions<T>, không inject IConfiguration
public class MyService(IOptions<MyServiceSettings> options) : IMyService
{
    private readonly MyServiceSettings _settings = options.Value;
    // ... dùng _settings.ApiKey, _settings.TimeoutSeconds
}
```

> **Tại sao**: `IConfiguration` là low-level infrastructure. Service inject nó trực tiếp = phụ thuộc vào structure của `appsettings.json`. Nếu đổi tên key → lỗi runtime. `IOptions<T>` = typed, compile-time safe, dễ mock trong test.

## Khi nào tạo extension method

Chỉ tạo extension method khi `Program.cs` có >50 dòng DI registration.

### Template AddApplication

`src/Beacon.Application/DependencyInjection/ApplicationServiceExtensions.cs`

```csharp
using Beacon.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Beacon.Application.DependencyInjection;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ApplicationAssemblyMarker).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(ApplicationAssemblyMarker).Assembly);

        return services;
    }
}
```

### Template AddInfrastructure

`src/Beacon.Infrashtructure/Dependencyinjection/InfrastructureServiceExtensions.cs`

```csharp
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Beacon.Infrashtructure.Dependencyinjection;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // Thêm repositories tại đây khi cần
        // services.AddScoped<IUserRepository, UserRepository>();

        return services;
    }
}
```

Cập nhật `Program.cs` khi dùng extensions:
```csharp
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
```
