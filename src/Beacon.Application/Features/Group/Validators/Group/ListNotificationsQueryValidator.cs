using Beacon.Application.Features.Group.Queries.ListNotifications;
using FluentValidation;

namespace Beacon.Application.Features.Group.Validators.Group;

public class ListNotificationsQueryValidator : AbstractValidator<ListNotificationsQuery>
{
    public ListNotificationsQueryValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50)
            .WithMessage("Limit phải từ 1 đến 50.");
    }
}
