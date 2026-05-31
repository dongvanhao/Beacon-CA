using Beacon.Application.Features.Authorization.Roles.Commands.AssignPermissionToRole;
using FluentValidation;

namespace Beacon.Application.Features.Authorization.Roles.Validators;

public class AssignPermissionToRoleCommandValidator : AbstractValidator<AssignPermissionToRoleCommand>
{
    public AssignPermissionToRoleCommandValidator()
    {
        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("RoleId không được để trống.");

        RuleFor(x => x.PermissionId)
            .NotEmpty().WithMessage("PermissionId không được để trống.");
    }
}
