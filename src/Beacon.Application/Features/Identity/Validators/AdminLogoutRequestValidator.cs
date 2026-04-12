using Beacon.Application.Features.Identity.Dtos;
using FluentValidation;

namespace Beacon.Application.Features.Identity.Validators;

public class AdminLogoutRequestValidator : AbstractValidator<AdminLogoutRequest>
{
    public AdminLogoutRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}
