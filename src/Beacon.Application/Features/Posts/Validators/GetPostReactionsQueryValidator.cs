using Beacon.Application.Features.Posts.Queries.GetPostReactions;
using Beacon.Domain.Enums;
using FluentValidation;

namespace Beacon.Application.Features.Posts.Validators;

public class GetPostReactionsQueryValidator : AbstractValidator<GetPostReactionsQuery>
{
    public GetPostReactionsQueryValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 100)
            .WithMessage("Limit must be between 1 and 100.");

        RuleFor(x => x.Icon)
            .Must(icon => icon == null || ReactionIcons.IsValid(icon))
            .WithMessage($"Icon must be at most {ReactionIcons.MaxIconLength} characters and must not contain the '{ReactionIcons.Separator}' separator.");

        RuleFor(x => x.Cursor)
            .Must(cursor => cursor == null || DateTime.TryParse(cursor, out _))
            .WithMessage("Cursor must be a valid ISO-8601 UTC datetime.");
    }
}
