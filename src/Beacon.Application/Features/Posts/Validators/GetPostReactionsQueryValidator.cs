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
            .WithMessage("Số lượng kết quả phải từ 1 đến 100.");

        RuleFor(x => x.Icon)
            .Must(icon => icon == null || ReactionIcons.IsValid(icon))
            .WithMessage($"Icon không hợp lệ. Chỉ chấp nhận: {string.Join(", ", ReactionIcons.Supported)}.");

        RuleFor(x => x.Cursor)
            .Must(cursor => cursor == null || DateTime.TryParse(cursor, out _))
            .WithMessage("Cursor phải là định dạng ISO-8601 UTC datetime hợp lệ.");
    }
}
