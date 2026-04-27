using Beacon.Application.Features.Checkins.Commands.CreateCheckin;
using Beacon.Domain.Enums.Checkins;
using FluentValidation;

namespace Beacon.Application.Features.Checkins.Validators.Checkins;

public class CreateCheckinCommandValidator : AbstractValidator<CreateCheckinCommand>
{
    public CreateCheckinCommandValidator()
    {
        RuleFor(x => x.Request.Type)
            .IsInEnum()
            .WithMessage("Loại check-in không hợp lệ. Các giá trị cho phép: Manual (1), Recovery (2), Emergency (3).");

        RuleFor(x => x.Request.Note)
            .MaximumLength(1000)
            .WithMessage("Ghi chú không được vượt quá 1000 ký tự.")
            .When(x => x.Request.Note is not null);

        RuleFor(x => x.Request.Latitude)
            .InclusiveBetween(-90m, 90m)
            .WithMessage("Vĩ độ phải nằm trong khoảng -90 đến 90.")
            .When(x => x.Request.Latitude.HasValue);

        RuleFor(x => x.Request.Longitude)
            .InclusiveBetween(-180m, 180m)
            .WithMessage("Kinh độ phải nằm trong khoảng -180 đến 180.")
            .When(x => x.Request.Longitude.HasValue);

        // Latitude và Longitude phải đi kèm nhau
        RuleFor(x => x.Request.Longitude)
            .NotNull()
            .WithMessage("Kinh độ không được để trống khi có vĩ độ.")
            .When(x => x.Request.Latitude.HasValue);

        RuleFor(x => x.Request.Latitude)
            .NotNull()
            .WithMessage("Vĩ độ không được để trống khi có kinh độ.")
            .When(x => x.Request.Longitude.HasValue);
    }
}
