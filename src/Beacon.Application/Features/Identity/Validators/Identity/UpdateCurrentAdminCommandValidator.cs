using Beacon.Application.Features.Identity.Commands;
using FluentValidation;

namespace Beacon.Application.Features.Identity.Validators.Identity;

public class UpdateCurrentAdminCommandValidator : AbstractValidator<UpdateCurrentAdminCommand>
{
    public UpdateCurrentAdminCommandValidator()
    {
        RuleFor(x => x.AdminId)
            .NotEmpty().WithMessage("AdminId khong duoc de trong.");

        RuleFor(x => x.Request)
            .NotNull().WithMessage("Body khong duoc de trong.")
            .Must(request => request is not null && (!string.IsNullOrWhiteSpace(request.Username)
                || !string.IsNullOrWhiteSpace(request.FullName))
            )
            .WithMessage("Can truyen it nhat username hoac fullName.");

        RuleFor(x => x.Request.Username)
            .MinimumLength(3).WithMessage("Username phai co it nhat 3 ky tu.")
            .MaximumLength(50).WithMessage("Username khong duoc vuot qua 50 ky tu.")
            .Matches(@"^[a-zA-Z0-9_\.]+$")
            .WithMessage("Username chi duoc chua chu cai, so, dau gach duoi va dau cham.")
            .When(x => x.Request is not null && !string.IsNullOrWhiteSpace(x.Request.Username));

        RuleFor(x => x.Request.FullName)
            .MaximumLength(200).WithMessage("Ho ten khong duoc vuot qua 200 ky tu.")
            .When(x => x.Request is not null && !string.IsNullOrWhiteSpace(x.Request.FullName));
    }
}
