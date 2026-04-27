using Beacon.Application.Features.Settings.Commands.UpdateSafetySetting;
using FluentValidation;

namespace Beacon.Application.Features.Settings.Validators;

public class UpdateSafetySettingCommandValidator : AbstractValidator<UpdateSafetySettingCommand>
{
    public UpdateSafetySettingCommandValidator()
    {
        RuleFor(x => x.Request.DailyDeadlineLocalTime)
            .NotEmpty().WithMessage("Giờ deadline không được để trống.")
            .Matches(@"^\d{2}:\d{2}$").WithMessage("Giờ deadline phải theo định dạng HH:mm.")
            .Must(t => TimeOnly.TryParse(t, out _)).WithMessage("Giờ deadline không hợp lệ.");

        RuleFor(x => x.Request.GracePeriodMinutes)
            .InclusiveBetween(0, 120)
            .WithMessage("Thời gian ân hạn phải từ 0 đến 120 phút.");

        RuleFor(x => x.Request.ReminderBeforeMinutes)
            .InclusiveBetween(0, 120)
            .WithMessage("Thời gian nhắc nhở phải từ 0 đến 120 phút.");

        RuleFor(x => x.Request.AutoAlertDelayMinutes)
            .InclusiveBetween(0, 60)
            .WithMessage("Thời gian chờ cảnh báo phải từ 0 đến 60 phút.");
    }
}
