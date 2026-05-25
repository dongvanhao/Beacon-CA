using Beacon.Application.Features.Messaging.Commands.CreateGroup;
using FluentValidation;

namespace Beacon.Application.Features.Messaging.Validators.Messaging;

public class CreateGroupCommandValidator : AbstractValidator<CreateGroupCommand>
{
    public CreateGroupCommandValidator()
    {
        RuleFor(x => x.MemberUserIds)
            .NotNull()
            .Must(ids => ids.Count > 0)
            .WithMessage("Danh sach thanh vien khong duoc rong.");

        RuleForEach(x => x.MemberUserIds)
            .NotEmpty();
    }
}
