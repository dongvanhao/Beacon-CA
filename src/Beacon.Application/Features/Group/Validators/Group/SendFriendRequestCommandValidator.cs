using Beacon.Application.Features.Group.Commands.SendFriendRequest;
using FluentValidation;

namespace Beacon.Application.Features.Group.Validators.Group;

public class SendFriendRequestCommandValidator : AbstractValidator<SendFriendRequestCommand>
{
    public SendFriendRequestCommandValidator()
    {
        RuleFor(x => x.ReceiverId)
            .NotEmpty().WithMessage("Id người nhận không được để trống.");
    }
}
