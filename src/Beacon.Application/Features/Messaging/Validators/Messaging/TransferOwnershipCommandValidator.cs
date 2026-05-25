using Beacon.Application.Features.Messaging.Commands.TransferOwnership;
using FluentValidation;

namespace Beacon.Application.Features.Messaging.Validators.Messaging;

public class TransferOwnershipCommandValidator : AbstractValidator<TransferOwnershipCommand>
{
    public TransferOwnershipCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.TargetUserId).NotEmpty();
        RuleFor(x => x.Role).IsInEnum();
    }
}
