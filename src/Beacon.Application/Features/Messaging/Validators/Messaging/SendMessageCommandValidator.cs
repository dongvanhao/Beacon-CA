using Beacon.Application.Features.Messaging.Commands.SendMessage;
using FluentValidation;

namespace Beacon.Application.Features.Messaging.Validators.Messaging;

public class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.GroupId.HasValue || x.PostId.HasValue)
            .WithMessage("Can truyen groupId hoac postId de gui tin nhan.");

        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage("Noi dung tin nhan khong duoc de trong.")
            .When(x => !x.PostId.HasValue);

        RuleFor(x => x.Content)
            .MaximumLength(4000)
            .WithMessage("Noi dung khong duoc vuot qua 4000 ky tu.");
    }
}
