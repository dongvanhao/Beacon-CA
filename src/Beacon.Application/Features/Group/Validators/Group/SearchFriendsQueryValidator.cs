using Beacon.Application.Features.Group.Queries.SearchFriends;
using FluentValidation;

namespace Beacon.Application.Features.Group.Validators.Group;

public class SearchFriendsQueryValidator : AbstractValidator<SearchFriendsQuery>
{
    public SearchFriendsQueryValidator()
    {
        RuleFor(x => x.Search)
            .NotEmpty().WithMessage("Từ khóa tìm kiếm không được để trống.")
            .MinimumLength(3).WithMessage("Từ khóa tìm kiếm phải có ít nhất 3 ký tự.");
    }
}
