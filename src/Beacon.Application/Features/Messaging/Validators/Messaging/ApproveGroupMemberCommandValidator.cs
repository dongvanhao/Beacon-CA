using Beacon.Application.Features.Messaging.Commands.ApproveGroupMember;
using FluentValidation;

namespace Beacon.Application.Features.Messaging.Validators.Messaging;

public class ApproveGroupMemberCommandValidator : AbstractValidator<ApproveGroupMemberCommand>
{
    public ApproveGroupMemberCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.TargetUserId).NotEmpty();
    }
}
