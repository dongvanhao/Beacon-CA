using Beacon.Application.Features.Group.Queries.SearchUsers;
using FluentValidation;

namespace Beacon.Application.Features.Group.Validators.Group;

public class SearchUsersQueryValidator : AbstractValidator<SearchUsersQuery>
{
    public SearchUsersQueryValidator()
    {
        RuleFor(x => x.Search)
            .NotEmpty().WithMessage("Từ khoá tìm kiếm không được để trống.")
            .MaximumLength(100).WithMessage("Từ khoá tìm kiếm không được vượt quá 100 ký tự.");
    }
}
