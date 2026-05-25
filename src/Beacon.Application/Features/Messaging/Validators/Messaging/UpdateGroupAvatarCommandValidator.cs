using Beacon.Application.Features.Messaging.Commands.UpdateGroupAvatar;
using FluentValidation;

namespace Beacon.Application.Features.Messaging.Validators.Messaging;

public class UpdateGroupAvatarCommandValidator : AbstractValidator<UpdateGroupAvatarCommand>
{
    private const long MaxImageBytes = 10L * 1024 * 1024; // 10 MB

    private static readonly string[] AllowedImageMimes =
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    };

    public UpdateGroupAvatarCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();

        RuleFor(x => x.File)
            .NotNull().WithMessage("File ảnh không được để trống.")
            .Must(f => f is not null && f.Length > 0).WithMessage("File ảnh không được rỗng.");

        RuleFor(x => x.File.ContentType)
            .NotEmpty()
            .Must(ct => AllowedImageMimes.Contains(ct?.ToLowerInvariant()))
            .WithMessage("Avatar nhóm phải là file ảnh (jpeg, png, webp, gif).")
            .When(x => x.File is not null);

        RuleFor(x => x.File)
            .Must(f => f is not null && f.Length <= MaxImageBytes)
            .WithMessage("Ảnh avatar nhóm không được vượt quá 10MB.")
            .When(x => x.File is not null);
    }
}
