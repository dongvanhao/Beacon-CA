using Beacon.Application.Features.Group.Commands.UpdateFriendType;
using FluentValidation;

namespace Beacon.Application.Features.Group.Validators.Group;

public class UpdateFriendTypeCommandValidator : AbstractValidator<UpdateFriendTypeCommand>
{
    public UpdateFriendTypeCommandValidator()
    {
        RuleFor(x => x.TargetUserId)
            .NotEmpty().WithMessage("Id người dùng không được để trống.");

        RuleFor(x => x.NewType)
            .IsInEnum().WithMessage("Loại bạn bè không hợp lệ.");
    }
}
