using Beacon.Application.Features.Checkins.Dtos;
using Beacon.Application.Mappings.Checkins;
using Beacon.Domain.Enums.Safety;
using Beacon.Domain.IRepository.Checkins;
using Beacon.Domain.IRepository.Safety;
using Beacon.Domain.IRepository.Settings;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Checkins.Queries.GetTodayCheckinStatus;

public class GetTodayCheckinStatusQueryHandler(
    IDailySafetyRecordRepository dailySafetyRecordRepo,
    ISafetySettingRepository safetySettingRepo,
    ICheckinRepository checkinRepo,
    CheckinStatusMapper mapper)
    : IRequestHandler<GetTodayCheckinStatusQuery, Result<TodayCheckinStatusDto>>
{
    public async Task<Result<TodayCheckinStatusDto>> Handle(
        GetTodayCheckinStatusQuery query, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var record  = await dailySafetyRecordRepo.GetByUserIdAndDateAsync(query.UserId, today, ct);
        var setting = await safetySettingRepo.GetByUserIdAsync(query.UserId, ct);
        var streak  = await checkinRepo.GetStreakAsync(query.UserId, today, ct);

        var deadlineAtUtc = record is not null
            ? record.DeadlineAtUtc
            : today.ToDateTime(setting?.DailyDeadlineLocalTime ?? new TimeOnly(23, 59), DateTimeKind.Utc);

        var isMonitoringEnabled = setting?.IsMonitoringEnabled ?? true;
        var isAutoAlertEnabled  = setting?.IsAutoAlertEnabled  ?? true;
        var hasCheckedIn        = record?.Status is SafetyStatus.CheckedIn or SafetyStatus.Resolved;

        return Result<TodayCheckinStatusDto>.Success(
            mapper.ToStatusDto(deadlineAtUtc, record?.CheckedInAtUtc, hasCheckedIn,
                isMonitoringEnabled, isAutoAlertEnabled, streak));
    }
}
