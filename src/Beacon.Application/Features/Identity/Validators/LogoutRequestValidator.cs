using Beacon.Application.Features.Identity.Commands;
using FluentValidation;

namespace Beacon.Application.Features.Identity.Validators;

/// <summary>
/// Validator cho LogoutCommand.
/// LogoutCommand(string RefreshToken) — property trực tiếp, không bọc DTO.
/// Target Command để ValidationBehavior pipeline có thể intercept.
/// </summary>
public class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token không được để trống.");
    }
}
