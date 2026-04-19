using Beacon.Application.Features.Identity.Commands;
using FluentValidation;

namespace Beacon.Application.Features.Identity.Validators;

/// <summary>
/// Validator cho LoginCommand.
/// Target Command để ValidationBehavior pipeline có thể intercept.
/// </summary>
public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        // Login validator cố ý giữ nhẹ — không kiểm tra format hay complexity
        // vì user đã có tài khoản, validate quá chặt sẽ chặn login hợp lệ.
        // MaximumLength trên Password chống DoS: bcrypt rất chậm với input lớn.

        RuleFor(x => x.Request.Username)
            .NotEmpty().WithMessage("Tên đăng nhập không được để trống.")
            .MaximumLength(50).WithMessage("Tên đăng nhập không được vượt quá 50 ký tự.");

        RuleFor(x => x.Request.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MaximumLength(100).WithMessage("Mật khẩu không được vượt quá 100 ký tự.");
    }
}
