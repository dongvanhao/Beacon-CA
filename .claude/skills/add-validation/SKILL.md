---
name: add-validation
description: Thêm FluentValidation validator cho một request DTO trong Beacon.Application
---

# Skill: Thêm FluentValidation

## Context

`ValidationBehavior<TRequest, TResponse>` đã được wire sẵn như MediatR pipeline trong `Program.cs`.
Validators auto-discovered qua `AddValidatorsFromAssembly(typeof(ApplicationAssemblyMarker).Assembly)`.
→ Chỉ cần tạo class validator, **KHÔNG cần đăng ký thủ công**.

## File location

```
src/Beacon.Application/Features/{Module}/Validators/{RequestName}Validator.cs
```

## Template

```csharp
using FluentValidation;
using Beacon.Application.Features.{Module}.Dtos;

namespace Beacon.Application.Features.{Module}.Validators;

public class {RequestName}Validator : AbstractValidator<{RequestName}>
{
    public {RequestName}Validator()
    {
        RuleFor(x => x.{RequiredField})
            .NotEmpty()
            .WithMessage("{RequiredField} is required.");

        RuleFor(x => x.Email)
            .EmailAddress()
            .WithMessage("Invalid email format.")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Name)
            .MaximumLength(100)
            .WithMessage("Name must not exceed 100 characters.");
    }
}
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
Request → ValidationBehavior → validators.Any() → Validate() → errors found
→ throw ValidationException(errors)
→ ExceptionHandlingMiddleware catches
→ HTTP 400 Bad Request
→ ApiResponse { success: false, code: "VALIDATION_ERROR", errors: ["msg1", "msg2"] }
```

## Ví dụ thực tế

```csharp
public class CreateEmergencyContactRequestValidator
    : AbstractValidator<CreateEmergencyContactRequest>
{
    public CreateEmergencyContactRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Contact name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("Invalid phone number format.");

        RuleFor(x => x.Channel)
            .IsInEnum().WithMessage("Invalid contact channel.");
    }
}
```
