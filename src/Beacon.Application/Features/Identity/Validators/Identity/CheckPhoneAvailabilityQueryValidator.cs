using Beacon.Application.Features.Identity.Queries;
using FluentValidation;

namespace Beacon.Application.Features.Identity.Validators;

public class CheckPhoneAvailabilityQueryValidator : AbstractValidator<CheckPhoneAvailabilityQuery>
{
    public CheckPhoneAvailabilityQueryValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("Số điện thoại không hợp lệ.");
    }
}
