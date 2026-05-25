using Beacon.Application.Features.Messaging.Commands.UpdateGroupName;
using FluentValidation;

namespace Beacon.Application.Features.Messaging.Validators.Messaging;

public class UpdateGroupNameCommandValidator : AbstractValidator<UpdateGroupNameCommand>
{
    public UpdateGroupNameCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.Name)
            .NotEmpty()
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Tên nhóm không được để trống.")
            .MaximumLength(100);
    }
}
