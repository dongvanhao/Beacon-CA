using Beacon.Application.Features.Authorization.Roles.Commands.CreateRole;
using FluentValidation;

namespace Beacon.Application.Features.Authorization.Roles.Validators;

public class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Request.Name)
            .NotEmpty().WithMessage("Tên role không được để trống.")
            .MaximumLength(100).WithMessage("Tên role không được vượt quá 100 ký tự.");

        RuleFor(x => x.Request.Description)
            .MaximumLength(500).WithMessage("Mô tả role không được vượt quá 500 ký tự.")
            .When(x => x.Request.Description is not null);
        RuleForEach(x => x.Request.PermissionIds)
            .NotEmpty().WithMessage("PermissionId khong duoc de trong.")
            .When(x => x.Request.PermissionIds is not null);
    }
}
