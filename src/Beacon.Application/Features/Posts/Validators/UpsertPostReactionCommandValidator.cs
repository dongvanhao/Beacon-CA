using Beacon.Application.Features.Posts.Commands.UpsertReaction;
using Beacon.Domain.Enums;
using FluentValidation;

namespace Beacon.Application.Features.Posts.Validators;

public class UpsertPostReactionCommandValidator : AbstractValidator<UpsertPostReactionCommand>
{
    public UpsertPostReactionCommandValidator()
    {
        RuleFor(x => x.Icon)
            .NotEmpty().WithMessage("Icon is required.")
            .Must(ReactionIcons.IsValid)
            .WithMessage($"Icon must be at most {ReactionIcons.MaxIconLength} characters and must not contain the '{ReactionIcons.Separator}' separator.");
    }
}
