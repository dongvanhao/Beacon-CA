using Beacon.Application.Features.Storage.Queries.ListMedia;
using FluentValidation;

namespace Beacon.Application.Features.Storage.Validators.Storage;

public class ListMediaQueryValidator : AbstractValidator<ListMediaQuery>
{
    public ListMediaQueryValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 100)
            .WithMessage("Limit phải nằm trong khoảng 1-100.");

        RuleFor(x => x.CurrentUserId)
            .NotEqual(Guid.Empty).WithMessage("Người dùng chưa xác thực.");
    }
}
