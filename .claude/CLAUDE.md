# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build

# Run API
cd src/Beacon.Api && dotnet run

# Run all tests
dotnet test

# Run specific test project
dotnet test tests/Beacon.UnitTests
dotnet test tests/Beacon.IntergrationTests

# EF Core migrations (run from solution root)
dotnet ef migrations add <MigrationName> --project src/Beacon.Infrashtructure --startup-project src/Beacon.Api
dotnet ef database update --project src/Beacon.Infrashtructure --startup-project src/Beacon.Api
```

## Architecture

**Clean Architecture** with 5 layers, dependencies pointing inward (API → Application → Domain, Infrastructure → Domain):

```
Beacon.Api              → Controllers, Middleware, DI wiring
Beacon.Application      → Services, DTOs, Validators, feature folders
Beacon.Domain           → Entities, Enums, Repository interfaces (no framework deps)
Beacon.Infrashtructure  → EF Core DbContext, Repository impls, SQL Server
Beacon.Shared           → Result<T>, ApiResponse<T>, Guards, Pagination, Constants
```

Note: "Infrashtructure" is an intentional typo in the project name — do not rename it.

## Result Pattern (mandatory)

All service methods must return `Result` or `Result<T>` from `Beacon.Shared`. Never throw exceptions for expected business failures.

```csharp
// Service layer
public async Task<Result<UserDto>> GetUserAsync(Guid id)
{
    var user = await _repo.GetByIdAsync(id);
    if (user is null) return Result.Failure<UserDto>(Error.NotFound(ErrorCodes.USER_NOT_FOUND, "User not found"));
    return Result.Success(_mapper.Map<UserDto>(user));
}

// Controller layer — use BaseController helpers
public async Task<IActionResult> GetUser(Guid id)
    => HandleResult(await _userService.GetUserAsync(id));
    // or for POST: CreatedResult("route", await _service.CreateAsync(dto))
```

## API Response Shape

All endpoints return `ApiResponse<T>` (from `Beacon.Shared`):
```json
{ "success": true, "message": "...", "code": null, "data": {}, "errors": null }
```

## Entity Base Classes

Choose the correct base for new entities:
- `BaseEntity` — just a `Guid Id`
- `AuditableEntity` — adds `CreatedAtUtc`, `UpdatedAtUtc`
- `SoftDeletableEntity` — adds `IsDeleted` with EF query filter

## Domain Modules

Feature folders exist in both `Application` and `Domain` for:
- **Identity** — users, JWT auth, refresh tokens, devices, roles (`User`, `Admin`)
- **Safety** — daily records, emergency contacts, alert incidents
- **Checkins** — checkin records with media attachments
- **Notification** — delivery tracking, multi-channel (email, SMS, push)
- **Settings** — per-user safety, notification, and app preferences
- **Storage** — media objects with public/private access control
- **Group**, **Messaging** — scaffolding only, not yet implemented

## EF Core Conventions

- Configurations use Fluent API in `Infrashtructure/Presistence/Configuration/`
- Soft-delete query filters are applied in `AppDbContext.OnModelCreating`, NOT in the config class
- Register new `IEntityTypeConfiguration<T>` classes — they are auto-discovered via `ApplyConfigurationsFromAssembly`
- Always add `DbSet<T>` to `AppDbContext` when creating a new entity

## Exception Handling

Global middleware in `Beacon.Api` maps exceptions to HTTP codes:
- `NotFoundException` → 404, `ConflictException` → 409, `UnauthorizedException` → 401, `ForbiddenException` → 403, `ValidationException` → 400, unhandled → 500

Throw these custom exceptions only for unexpected/unrecoverable errors; use `Result.Failure` for all expected business failures.

## Repository Interfaces

Location: `src/Beacon.Domain/IRepository/`
- No `IRepository<T>` base interface exists yet — declare each interface directly
- Implementations go in `src/Beacon.Infrashtructure/Repository/{Module}/`

## DI Registration (current state)

All services registered inline in `src/Beacon.Api/Program.cs`.
Extension method folders (`DependencyInjection/`, `Dependencyinjection/`, `Extensions/`) exist but are empty.
For new repositories: `builder.Services.AddScoped<I{Resource}Repository, {Resource}Repository>();`

## Mapping

**Mapster is NOT INSTALLED.** Do not generate `TypeAdapterConfig` or `IRegister` code.
Use manual property assignment until Mapster is installed:
```csharp
var dto = new EntityDto { Id = entity.Id, Name = entity.Name };
```

## Namespace Quirk

- Folder `src/Beacon.Domain/Entities/Settings/` → namespace `Beacon.Domain.Entities.Setting` (no 's')
- Project name `Beacon.Infrashtructure` — intentional typo, do not rename
