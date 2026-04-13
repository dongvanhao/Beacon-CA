---
name: add-validation
description: Thêm FluentValidation validator cho một Command trong Beacon.Application
---

# Skill: Thêm FluentValidation

## Context

`ValidationBehavior<TRequest, TResponse>` đã được wire sẵn như MediatR pipeline trong `Program.cs`.
Validators auto-discovered qua `AddValidatorsFromAssembly(typeof(ApplicationAssemblyMarker).Assembly)`.
→ Chỉ cần tạo class validator, **KHÔNG cần đăng ký thủ công**.

> ⚠️ **Quan trọng**: `ValidationBehavior` inject `IValidator<TRequest>` — trong đó `TRequest` chính là **Command**,
> **không phải DTO** bên trong Command. Validator phải target **Command**, không target DTO,
> nếu không pipeline sẽ không tìm thấy validator và bỏ qua validation hoàn toàn.

## File location

```
src/Beacon.Application/Features/{Module}/Validators/{CommandName}Validator.cs
```

## Template

```csharp
using FluentValidation;
using Beacon.Application.Features.{Module}.Commands;

namespace Beacon.Application.Features.{Module}.Validators;

/// <summary>
/// Validator cho {CommandName}.
/// Target Command (không phải DTO) để ValidationBehavior pipeline có thể interceptt.
/// </summary>
public class {CommandName}Validator : AbstractValidator<{CommandName}>
{
    public {CommandName}Validator()
    {
        // Nếu Command bọc DTO qua property Request:
        RuleFor(x => x.Request.{RequiredField})
            .NotEmpty()
            .WithMessage("{RequiredField} is required.");

        RuleFor(x => x.Request.Email)
            .EmailAddress()
            .WithMessage("Invalid email format.")
            .When(x => !string.IsNullOrEmpty(x.Request.Email));

        RuleFor(x => x.Request.Name)
            .MaximumLength(100)
            .WithMessage("Name must not exceed 100 characters.");

        // Nếu Command có property trực tiếp (không bọc DTO):
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithMessage("Refresh token is required.");
    }
}
```

## Hai dạng Command phổ biến

### Dạng 1: Command bọc DTO
```csharp
// Command:
public record RegisterCommand(RegisterRequest Request, ...) : IRequest<Result<AuthResponse>>;

// → Validator access field qua .Request.{Field}
RuleFor(x => x.Request.Username).NotEmpty()...
```

### Dạng 2: Command có property trực tiếp
```csharp
// Command:
public record LogoutCommand(string RefreshToken) : IRequest<Result>;

// → Validator access field trực tiếp
RuleFor(x => x.RefreshToken).NotEmpty()...
```

## Rules phổ biến

| Rule | Dùng khi |
|---|---|
| `.NotEmpty()` | string/collection không được null hoặc rỗng |
| `.NotNull()` | reference type không được null |
| `.MaximumLength(n)` | giới hạn độ dài string |
| `.MinimumLength(n)` | độ dài tối thiểu |
| `.GreaterThan(0)` | số phải > 0 |
| `.InclusiveBetween(a, b)` | số trong khoảng [a, b] |
| `.EmailAddress()` | format email hợp lệ |
| `.Matches(@"regex")` | pattern matching |
| `.IsInEnum()` | enum value hợp lệ |
| `.Must(x => condition).WithMessage("msg")` | custom rule |
| `.When(condition)` | chỉ validate khi điều kiện đúng |

## Flow khi fail

```
Controller → mediator.Send(Command)
  → ValidationBehavior<Command, Result>
    → IValidator<Command> found → Validate(command)
    → errors found → throw ValidationException(errors)
  → ExceptionHandlingMiddleware catches
  → HTTP 400 Bad Request
  → ApiResponse { success: false, code: "VALIDATION_ERROR", errors: ["msg1", "msg2"] }
```

## Ví dụ thực tế

```csharp
// Command: CreateEmergencyContactCommand(CreateEmergencyContactRequest Request)
public class CreateEmergencyContactCommandValidator
    : AbstractValidator<CreateEmergencyContactCommand>
{
    public CreateEmergencyContactCommandValidator()
    {
        RuleFor(x => x.Request.Name)
            .NotEmpty().WithMessage("Contact name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.Request.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("Invalid phone number format.");

        RuleFor(x => x.Request.Channel)
            .IsInEnum().WithMessage("Invalid contact channel.");
    }
}
```
