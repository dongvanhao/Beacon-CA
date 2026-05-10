using Beacon.Application.Features.Group.Commands.CancelFriendRequest;
using FluentValidation;

namespace Beacon.Application.Features.Group.Validators.Group;

public class CancelFriendRequestCommandValidator : AbstractValidator<CancelFriendRequestCommand>
{
    public CancelFriendRequestCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
    }
}
