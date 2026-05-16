using Beacon.Application.Features.Posts.Commands.UpsertReaction;
using Beacon.Domain.Enums;
using FluentValidation;

namespace Beacon.Application.Features.Posts.Validators;

public class UpsertPostReactionCommandValidator : AbstractValidator<UpsertPostReactionCommand>
{
    public UpsertPostReactionCommandValidator()
    {
        RuleFor(x => x.Icon)
            .NotEmpty().WithMessage("Icon không được để trống.")
            .Must(ReactionIcons.IsValid)
            .WithMessage("Icon không hợp lệ. Chỉ hỗ trợ: heart, haha, like, sad, wow");
    }
}
