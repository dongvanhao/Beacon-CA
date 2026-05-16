using Beacon.Application.Features.Posts.Commands.UpdatePost;
using FluentValidation;

namespace Beacon.Application.Features.Posts.Validators;

public class UpdatePostCommandValidator : AbstractValidator<UpdatePostCommand>
{
    private static readonly string[] ValidVisibilities = { "friends", "private" };

    public UpdatePostCommandValidator()
    {
        RuleFor(x => x.Request.Caption)
            .MaximumLength(500).When(x => x.Request.Caption != null)
            .WithMessage("Caption không được vượt quá 500 ký tự.");

        RuleFor(x => x.Request.Visibility)
            .Must(v => v == null || ValidVisibilities.Contains(v.ToLowerInvariant()))
            .WithMessage("Visibility không hợp lệ. Chỉ hỗ trợ: 'friends' hoặc 'private'.");
    }
}
