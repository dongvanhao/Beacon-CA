using Beacon.Application.Features.Identity.Commands;
using FluentValidation;

namespace Beacon.Application.Features.Identity.Validators;

public class RefreshAdminTokenCommandValidator : AbstractValidator<RefreshAdminTokenCommand>
{
    public RefreshAdminTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token không được để trống.");
    }
}
