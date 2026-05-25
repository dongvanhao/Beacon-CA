using Beacon.Application.Features.Messaging.Commands.UpdateGroupMemberCustomName;
using FluentValidation;

namespace Beacon.Application.Features.Messaging.Validators.Messaging;

public class UpdateGroupMemberCustomNameCommandValidator : AbstractValidator<UpdateGroupMemberCustomNameCommand>
{
    public UpdateGroupMemberCustomNameCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.TargetUserId).NotEmpty();
        RuleFor(x => x.CustomName)
            .MaximumLength(100)
            .WithMessage("Biệt danh không được vượt quá 100 ký tự.");
    }
}
