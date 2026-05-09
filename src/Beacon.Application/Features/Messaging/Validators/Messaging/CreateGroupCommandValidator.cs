using Beacon.Application.Features.Messaging.Commands.CreateGroup;
using FluentValidation;

namespace Beacon.Application.Features.Messaging.Validators.Messaging;

public class CreateGroupCommandValidator : AbstractValidator<CreateGroupCommand>
{
    public CreateGroupCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
