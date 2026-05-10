using Beacon.Application.Features.Identity.Commands.RevokeDeviceToken;
using FluentValidation;

namespace Beacon.Application.Features.Identity.Validators.Identity;

public class RevokeDeviceTokenCommandValidator : AbstractValidator<RevokeDeviceTokenCommand>
{
    public RevokeDeviceTokenCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Token không được để trống.");
    }
}
