using Beacon.Application.Features.Identity.Queries;
using FluentValidation;

namespace Beacon.Application.Features.Identity.Validators;

public class CheckEmailAvailabilityQueryValidator : AbstractValidator<CheckEmailAvailabilityQuery>
{
    public CheckEmailAvailabilityQueryValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .MaximumLength(254).WithMessage("Email không được vượt quá 254 ký tự.")
            .EmailAddress().WithMessage("Email không hợp lệ.");
    }
}
