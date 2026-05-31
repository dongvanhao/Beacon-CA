using Beacon.Application.Features.Authorization.Permissions.Commands.CreatePermission;
using FluentValidation;

namespace Beacon.Application.Features.Authorization.Permissions.Validators;

public class CreatePermissionCommandValidator : AbstractValidator<CreatePermissionCommand>
{
    public CreatePermissionCommandValidator()
    {
        RuleFor(x => x.Request.Name)
            .NotEmpty().WithMessage("Tên permission không được để trống.")
            .MaximumLength(100).WithMessage("Tên permission không được vượt quá 100 ký tự.");

        RuleFor(x => x.Request.Description)
            .MaximumLength(500).WithMessage("Mô tả permission không được vượt quá 500 ký tự.")
            .When(x => x.Request.Description is not null);

        RuleFor(x => x.Request.Group)
            .MaximumLength(100).WithMessage("Nhóm permission không được vượt quá 100 ký tự.")
            .When(x => x.Request.Group is not null);
    }
}
