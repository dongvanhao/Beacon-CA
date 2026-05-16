using Beacon.Application.Features.Posts.Commands.CreatePost;
using FluentValidation;

namespace Beacon.Application.Features.Posts.Validators;

public class CreatePostCommandValidator : AbstractValidator<CreatePostCommand>
{
    private static readonly string[] ValidVisibilities = { "friends", "private" };

    public CreatePostCommandValidator()
    {
        RuleFor(x => x.Request.MediaId)
            .NotEmpty().WithMessage("MediaId là bắt buộc.");

        RuleFor(x => x.Request.Caption)
            .MaximumLength(500).When(x => x.Request.Caption != null)
            .WithMessage("Caption không được vượt quá 500 ký tự.");

        RuleFor(x => x.Request.Visibility)
            .Must(v => v == null || ValidVisibilities.Contains(v.ToLowerInvariant()))
            .WithMessage("Visibility không hợp lệ. Chỉ hỗ trợ: 'friends' hoặc 'private'.");
    }
}
