using Beacon.Application.Features.Identity.Commands.RegisterDeviceToken;
using Beacon.Domain.Enums.Identity;
using FluentValidation;

namespace Beacon.Application.Features.Identity.Validators.Identity;

public class RegisterDeviceTokenCommandValidator : AbstractValidator<RegisterDeviceTokenCommand>
{
    public RegisterDeviceTokenCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Token không được để trống.")
            .MaximumLength(1000).WithMessage("Token không được vượt quá 1000 ký tự.");

        RuleFor(x => x.Platform)
            .IsInEnum().WithMessage("Platform không hợp lệ.");
    }
}
