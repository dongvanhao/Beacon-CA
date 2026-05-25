using Beacon.Application.Features.Messaging.Commands.AddGroupMember;
using FluentValidation;

namespace Beacon.Application.Features.Messaging.Validators.Messaging;

public class AddGroupMemberCommandValidator : AbstractValidator<AddGroupMemberCommand>
{
    public AddGroupMemberCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.TargetUserIds)
            .NotEmpty()
            .WithMessage("Danh sách thành viên cần thêm không được để trống.");

        RuleForEach(x => x.TargetUserIds)
            .NotEmpty()
            .WithMessage("Id thành viên cần thêm không hợp lệ.");

        RuleFor(x => x.TargetUserIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Danh sách thành viên cần thêm không được trùng lặp.")
            .When(x => x.TargetUserIds is not null);
    }
}
