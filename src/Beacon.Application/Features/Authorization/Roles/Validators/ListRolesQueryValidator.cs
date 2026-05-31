using Beacon.Application.Features.Authorization.Roles.Queries.ListRoles;
using FluentValidation;

namespace Beacon.Application.Features.Authorization.Roles.Validators;

public class ListRolesQueryValidator : AbstractValidator<ListRolesQuery>
{
    public ListRolesQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page phải lớn hơn hoặc bằng 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize phải nằm trong khoảng 1-100.");

        RuleFor(x => x.Search)
            .MaximumLength(100)
            .WithMessage("Từ khóa tìm kiếm không được vượt quá 100 ký tự.")
            .When(x => x.Search is not null);
    }
}
