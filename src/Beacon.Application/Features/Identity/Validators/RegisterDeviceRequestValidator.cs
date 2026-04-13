using Beacon.Application.Features.Identity.Dtos;
using FluentValidation;

namespace Beacon.Application.Features.Identity.Validators;

public class RegisterDeviceRequestValidator : AbstractValidator<RegisterDeviceRequest>
{
    public RegisterDeviceRequestValidator()
    {
        RuleFor(x => x.DeviceToken)
            .NotEmpty().WithMessage("Device token is required.")
            .MaximumLength(500).WithMessage("Device token must not exceed 500 characters.");
    }
}
