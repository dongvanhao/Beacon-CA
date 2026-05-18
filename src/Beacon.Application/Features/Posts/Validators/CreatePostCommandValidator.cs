using Beacon.Application.Features.Posts.Commands.CreatePost;
using FluentValidation;

namespace Beacon.Application.Features.Posts.Validators;

public class CreatePostCommandValidator : AbstractValidator<CreatePostCommand>
{
    private static readonly string[] ValidVisibilities = { "friends", "private" };

    public CreatePostCommandValidator()
    {
        RuleFor(x => x.Request.MediaId)
            .NotEmpty().WithMessage("MediaId la bat buoc.");

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
