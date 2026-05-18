using Beacon.Application.Features.Posts.Commands.UpdatePost;
using FluentValidation;

namespace Beacon.Application.Features.Posts.Validators;

public class UpdatePostCommandValidator : AbstractValidator<UpdatePostCommand>
{
    private static readonly string[] ValidVisibilities = { "friends", "private" };

    public UpdatePostCommandValidator()
    {
        RuleFor(x => x.Request.Caption)
            .MaximumLength(500).When(x => x.Request.Caption != null)
            .WithMessage("Caption khong duoc vuot qua 500 ky tu.");

        RuleFor(x => x.Request.Visibility)
            .Must(v => v == null || ValidVisibilities.Contains(v.ToLowerInvariant()))
            .WithMessage("Visibility khong hop le. Chi ho tro: 'friends' hoac 'private'.");

        RuleFor(x => x.Request.Latitude)
            .InclusiveBetween(-90m, 90m)
            .WithMessage("Vi do phai nam trong khoang -90 den 90.")
            .When(x => x.Request.Latitude.HasValue);

        RuleFor(x => x.Request.Longitude)
            .InclusiveBetween(-180m, 180m)
            .WithMessage("Kinh do phai nam trong khoang -180 den 180.")
            .When(x => x.Request.Longitude.HasValue);

        RuleFor(x => x.Request.Longitude)
            .NotNull()
            .WithMessage("Kinh do khong duoc de trong khi co vi do.")
            .When(x => x.Request.Latitude.HasValue);

        RuleFor(x => x.Request.Latitude)
            .NotNull()
            .WithMessage("Vi do khong duoc de trong khi co kinh do.")
            .When(x => x.Request.Longitude.HasValue);
    }
}
