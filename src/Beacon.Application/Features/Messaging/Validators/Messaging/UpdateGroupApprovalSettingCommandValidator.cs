using Beacon.Application.Features.Messaging.Commands.UpdateGroupApprovalSetting;
using FluentValidation;

namespace Beacon.Application.Features.Messaging.Validators.Messaging;

public class UpdateGroupApprovalSettingCommandValidator : AbstractValidator<UpdateGroupApprovalSettingCommand>
{
    public UpdateGroupApprovalSettingCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
    }
}
