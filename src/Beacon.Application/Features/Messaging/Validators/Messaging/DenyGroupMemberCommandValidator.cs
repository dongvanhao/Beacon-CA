using Beacon.Application.Features.Messaging.Commands.DenyGroupMember;
using FluentValidation;

namespace Beacon.Application.Features.Messaging.Validators.Messaging;

public class DenyGroupMemberCommandValidator : AbstractValidator<DenyGroupMemberCommand>
{
    public DenyGroupMemberCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.TargetUserId).NotEmpty();
    }
}
