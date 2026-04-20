using Beacon.Application.Features.Identity.Commands.UpdateProfile;
using FluentValidation;

namespace Beacon.Application.Features.Identity.Validators.Identity;

public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        // Chỉ validate khi field được gửi lên (không null)
        RuleFor(x => x.Request.FamilyName)
            .NotEmpty().WithMessage("Họ không được để trống.")
            .MaximumLength(100).WithMessage("Họ không được vượt quá 100 ký tự.")
            .When(x => x.Request.FamilyName is not null);

        RuleFor(x => x.Request.GivenName)
            .NotEmpty().WithMessage("Tên không được để trống.")
            .MaximumLength(100).WithMessage("Tên không được vượt quá 100 ký tự.")
            .When(x => x.Request.GivenName is not null);

        RuleFor(x => x.Request.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không đúng định dạng.")
            .MaximumLength(256).WithMessage("Email không được vượt quá 256 ký tự.")
            .When(x => x.Request.Email is not null);

        RuleFor(x => x.Request.PhoneNumber)
            .Matches(@"^(0|\+?[1-9])\d{1,14}$").WithMessage("Số điện thoại không hợp lệ.")
            .When(x => !string.IsNullOrEmpty(x.Request.PhoneNumber));
    }
}
