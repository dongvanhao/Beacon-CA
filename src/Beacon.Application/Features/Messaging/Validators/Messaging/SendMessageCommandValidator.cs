using Beacon.Application.Features.Messaging.Commands.SendMessage;
using FluentValidation;

namespace Beacon.Application.Features.Messaging.Validators.Messaging;

public class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        RuleFor(x => x.GroupId)
            .NotEmpty().WithMessage("Id nhóm không được để trống.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Nội dung tin nhắn không được để trống.")
            .MaximumLength(4000).WithMessage("Nội dung không được vượt quá 4000 ký tự.");
    }
}
