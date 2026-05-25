using Beacon.Application.Features.Messaging.Commands.UpdateGroupMemberMuted;
using FluentValidation;

namespace Beacon.Application.Features.Messaging.Validators.Messaging;

public class UpdateGroupMemberMutedCommandValidator : AbstractValidator<UpdateGroupMemberMutedCommand>
{
    public UpdateGroupMemberMutedCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
    }
}
