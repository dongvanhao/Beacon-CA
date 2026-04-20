using Beacon.Application.Features.Identity.Commands.UpdateAvatar;
using FluentValidation;

namespace Beacon.Application.Features.Identity.Validators.Identity;

public class UpdateAvatarCommandValidator : AbstractValidator<UpdateAvatarCommand>
{
    private const long MaxImageBytes = 10L * 1024 * 1024; // 10 MB

    private static readonly string[] AllowedImageMimes =
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    };

    public UpdateAvatarCommandValidator()
    {
        RuleFor(x => x.File)
            .NotNull().WithMessage("File ảnh không được để trống.")
            .Must(f => f is not null && f.Length > 0).WithMessage("File ảnh không được rỗng.");

        RuleFor(x => x.File.ContentType)
            .NotEmpty()
            .Must(ct => AllowedImageMimes.Contains(ct?.ToLowerInvariant()))
            .WithMessage("Avatar phải là file ảnh (jpeg, png, webp, gif).")
            .When(x => x.File is not null);

        RuleFor(x => x.File)
            .Must(f => f is not null && f.Length <= MaxImageBytes)
            .WithMessage("Ảnh avatar không được vượt quá 10MB.")
            .When(x => x.File is not null);

        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty).WithMessage("Người dùng chưa xác thực.");
    }
}
