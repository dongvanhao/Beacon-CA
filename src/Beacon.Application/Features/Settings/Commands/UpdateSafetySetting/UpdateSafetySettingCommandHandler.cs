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
        var req      = cmd.Request;
        var deadline = TimeOnly.Parse(req.DailyDeadlineLocalTime);

        var setting = await repo.GetByUserIdAsync(cmd.UserId, ct);

        if (setting is null)
        {
            setting = SafetySetting.CreateDefault(cmd.UserId, deadline);
            setting.UpdateSettings(deadline, req.GracePeriodMinutes, req.ReminderBeforeMinutes,
                req.AutoAlertDelayMinutes, req.IsMonitoringEnabled, req.IsAutoAlertEnabled);
            await repo.AddAsync(setting, ct);
        }
        else
        {
            setting.UpdateSettings(deadline, req.GracePeriodMinutes, req.ReminderBeforeMinutes,
                req.AutoAlertDelayMinutes, req.IsMonitoringEnabled, req.IsAutoAlertEnabled);
        }

        await repo.SaveChangesAsync(ct);
        return Result<SafetySettingDto>.Success(mapper.ToDto(setting));
    }
}
