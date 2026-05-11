using Beacon.Application.Features.Group.Queries.ListFriendPresence;
using FluentValidation;

namespace Beacon.Application.Features.Group.Validators.Group;

public class ListFriendPresenceQueryValidator : AbstractValidator<ListFriendPresenceQuery>
{
    public ListFriendPresenceQueryValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 100)
            .WithMessage("Limit phải từ 1 đến 100.");
    }
}
