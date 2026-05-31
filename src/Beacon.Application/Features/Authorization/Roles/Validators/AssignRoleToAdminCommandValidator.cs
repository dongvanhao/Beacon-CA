using Beacon.Application.Features.Authorization.Roles.Commands.AssignRoleToAdmin;
using FluentValidation;

namespace Beacon.Application.Features.Authorization.Roles.Validators;

public class AssignRoleToAdminCommandValidator : AbstractValidator<AssignRoleToAdminCommand>
{
    public AssignRoleToAdminCommandValidator()
    {
        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("RoleId không được để trống.");

        RuleFor(x => x.AdminId)
            .NotEmpty().WithMessage("AdminId không được để trống.");
    }
}
