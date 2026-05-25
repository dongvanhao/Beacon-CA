using Beacon.Application.Features.Safety.Commands.CreateEmergencyContact;
using FluentValidation;

namespace Beacon.Application.Features.Safety.Validators.Safety;

public class CreateEmergencyContactCommandValidator : AbstractValidator<CreateEmergencyContactCommand>
{
    public CreateEmergencyContactCommandValidator()
    {
        RuleFor(x => x.Request.FullName)
            .NotEmpty().WithMessage("Họ tên không được để trống.")
            .MaximumLength(200).WithMessage("Họ tên không được vượt quá 200 ký tự.");

        RuleFor(x => x.Request.ContactValue)
            .NotEmpty().WithMessage("Thông tin liên hệ không được để trống.")
            .MaximumLength(255).WithMessage("Thông tin liên hệ không được vượt quá 255 ký tự.");

        RuleFor(x => x.Request.ChannelType)
            .IsInEnum().WithMessage("Loại kênh liên hệ không hợp lệ.");

        RuleFor(x => x.Request.Relationship)
            .MaximumLength(100).WithMessage("Quan hệ không được vượt quá 100 ký tự.")
            .When(x => x.Request.Relationship is not null);

        RuleFor(x => x.Request.PriorityOrder)
            .InclusiveBetween(1, 99).WithMessage("Thứ tự ưu tiên phải trong khoảng 1–99.");
    }
}
