using Beacon.Application.Features.Messaging.Commands.AddGroupMember;
using FluentValidation;

namespace Beacon.Application.Features.Messaging.Validators.Messaging;

public class AddGroupMemberCommandValidator : AbstractValidator<AddGroupMemberCommand>
{
    public AddGroupMemberCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.TargetUserId).NotEmpty();
    }
}
