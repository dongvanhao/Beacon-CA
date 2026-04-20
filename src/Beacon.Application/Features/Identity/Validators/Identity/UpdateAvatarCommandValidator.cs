using Beacon.Application.Features.Identity.Commands.UpdateAvatar;
using FluentValidation;

namespace Beacon.Application.Features.Identity.Validators.Identity;

public class UpdateAvatarCommandValidator : AbstractValidator<UpdateAvatarCommand>
{
    public UpdateAvatarCommandValidator()
    {
        RuleFor(x => x.MediaObjectId)
            .NotEqual(Guid.Empty).WithMessage("MediaObjectId không hợp lệ.");
    }
}
