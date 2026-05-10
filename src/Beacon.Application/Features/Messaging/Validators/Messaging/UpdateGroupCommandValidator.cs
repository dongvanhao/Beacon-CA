using Beacon.Application.Features.Messaging.Commands.UpdateGroup;
using FluentValidation;

namespace Beacon.Application.Features.Messaging.Validators.Messaging;

public class UpdateGroupCommandValidator : AbstractValidator<UpdateGroupCommand>
{
    public UpdateGroupCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        When(x => x.Name is not null, () =>
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100));
    }
}
