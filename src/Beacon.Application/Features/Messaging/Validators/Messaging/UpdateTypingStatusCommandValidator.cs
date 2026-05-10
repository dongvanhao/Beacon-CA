using Beacon.Application.Features.Messaging.Commands.UpdateTypingStatus;
using FluentValidation;

namespace Beacon.Application.Features.Messaging.Validators.Messaging;

public class UpdateTypingStatusCommandValidator : AbstractValidator<UpdateTypingStatusCommand>
{
    public UpdateTypingStatusCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty().WithMessage("GroupId không được để trống.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("UserId không được để trống.");
    }
}
