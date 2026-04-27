using Beacon.Application.Features.Settings.Dtos;
using Beacon.Application.Mappings.Settings;
using Beacon.Domain.Entities.Setting;
using Beacon.Domain.IRepository.Settings;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Settings.Commands.UpdateSafetySetting;

public class UpdateSafetySettingCommandHandler(
    ISafetySettingRepository repo,
    SafetySettingMapper mapper)
    : IRequestHandler<UpdateSafetySettingCommand, Result<SafetySettingDto>>
{
    public async Task<Result<SafetySettingDto>> Handle(UpdateSafetySettingCommand cmd, CancellationToken ct)
    {
        var req     = cmd.Request;
        var setting = await repo.GetByUserIdAsync(cmd.UserId, ct);

        // Giữ nguyên giá trị cũ nếu field không được gửi lên; dùng default cho lần tạo đầu tiên
        var deadline     = req.DailyDeadlineLocalTime is not null
                               ? TimeOnly.Parse(req.DailyDeadlineLocalTime)
                               : setting?.DailyDeadlineLocalTime ?? new TimeOnly(8, 0);
        var gracePeriod  = req.GracePeriodMinutes      ?? setting?.GracePeriodMinutes      ?? 15;
        var reminder     = req.ReminderBeforeMinutes    ?? setting?.ReminderBeforeMinutes    ?? 30;
        var autoDelay    = req.AutoAlertDelayMinutes    ?? setting?.AutoAlertDelayMinutes    ?? 15;
        var isMonitoring = req.IsMonitoringEnabled      ?? setting?.IsMonitoringEnabled      ?? true;
        var isAutoAlert  = req.IsAutoAlertEnabled       ?? setting?.IsAutoAlertEnabled       ?? true;

        // IsAutoAlertEnabled không có nghĩa khi monitoring tắt — normalize trước khi lưu
        var effectiveAutoAlert = isMonitoring && isAutoAlert;

        if (setting is null)
        {
            setting = SafetySetting.CreateDefault(cmd.UserId, deadline);
            setting.UpdateSettings(deadline, gracePeriod, reminder, autoDelay, isMonitoring, effectiveAutoAlert);
            await repo.AddAsync(setting, ct);
        }
        else
        {
            setting.UpdateSettings(deadline, gracePeriod, reminder, autoDelay, isMonitoring, effectiveAutoAlert);
        }

        await repo.SaveChangesAsync(ct);
        return Result<SafetySettingDto>.Success(mapper.ToDto(setting));
    }
}
