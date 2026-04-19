using Beacon.Application.Features.Identity.Commands;
using FluentValidation;

namespace Beacon.Application.Features.Identity.Validators;

/// <summary>
/// Validator cho RegisterDeviceCommand.
/// Target Command để ValidationBehavior pipeline có thể intercept.
/// </summary>
public class RegisterDeviceCommandValidator : AbstractValidator<RegisterDeviceCommand>
{
    public RegisterDeviceCommandValidator()
    {
        RuleFor(x => x.Request.DeviceToken)
            .NotEmpty().WithMessage("Device token không được để trống.")
            .MaximumLength(500).WithMessage("Device token không được vượt quá 500 ký tự.");
    }
}
