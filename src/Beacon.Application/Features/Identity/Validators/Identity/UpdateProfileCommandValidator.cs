using Beacon.Application.Features.Identity.Commands.UpdateProfile;
using FluentValidation;

namespace Beacon.Application.Features.Identity.Validators.Identity;

public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.Request.FamilyName)
            .NotEmpty().WithMessage("Họ không được để trống.")
            .MaximumLength(100).WithMessage("Họ không được vượt quá 100 ký tự.");

        RuleFor(x => x.Request.GivenName)
            .NotEmpty().WithMessage("Tên không được để trống.")
            .MaximumLength(100).WithMessage("Tên không được vượt quá 100 ký tự.");

        RuleFor(x => x.Request.PhoneNumber)
            .Matches(@"^(0|\+?[1-9])\d{1,14}$").WithMessage("Số điện thoại không hợp lệ.")
            .When(x => !string.IsNullOrEmpty(x.Request.PhoneNumber));

        RuleFor(x => x.Request.TimeZone)
            .NotEmpty().WithMessage("Múi giờ không được để trống.")
            .MaximumLength(50).WithMessage("Múi giờ không được vượt quá 50 ký tự.");
    }
}
