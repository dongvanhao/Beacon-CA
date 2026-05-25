using Beacon.Application.Features.Checkins.Dtos;
using Beacon.Application.Mappings.Checkins;
using Beacon.Domain.Entities.Checkins;
using Beacon.Domain.Entities.Safety;
using Beacon.Domain.Enums.Checkins;
using Beacon.Domain.Enums.Safety;
using Beacon.Domain.IRepository.Checkins;
using Beacon.Domain.IRepository.Safety;
using Beacon.Domain.IRepository.Settings;
using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Checkins.Commands.CreateCheckin;

public class CreateCheckinCommandHandler(
    IDailySafetyRecordRepository dailySafetyRecordRepo,
    ISafetySettingRepository safetySettingRepo,
    IMediaObjectRepository mediaRepo,
    ICheckinRepository checkinRepo,
    CheckinMapper mapper)
    : IRequestHandler<CreateCheckinCommand, Result<CheckinDto>>
{
    private static readonly TimeZoneInfo VietnamTz = GetVietnamTimeZone();

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
    }

    public async Task<Result<CheckinDto>> Handle(CreateCheckinCommand cmd, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTz));

        var record = await dailySafetyRecordRepo.GetByUserIdAndDateWithIncidentAsync(cmd.UserId, today, ct);

        if (record is null)
        {
            var deadline = await ComputeDeadlineAsync(cmd.UserId, today, ct);
            record = DailySafetyRecord.Create(cmd.UserId, today, deadline);
            await dailySafetyRecordRepo.AddAsync(record, ct);
        }

        if (record.Status is SafetyStatus.CheckedIn or SafetyStatus.Resolved)
            return Result<CheckinDto>.Failure(
                Error.Conflict(ErrorCodes.Safety.ALREADY_CHECKED_IN, "Bạn đã check-in hôm nay rồi."));

        var req = cmd.Request;

        if (req.MediaId.HasValue)
        {
            var media = await mediaRepo.GetByIdAsync(req.MediaId.Value, ct);
            if (media is null)
                return Result<CheckinDto>.Failure(
                    Error.NotFound(ErrorCodes.Storage.MEDIA_NOT_FOUND, "Không tìm thấy media."));
        }

        // Nhánh A — Pending: checkin đúng hạn
        if (record.Status == SafetyStatus.Pending)
        {
            var checkin = Checkin.Create(cmd.UserId, record.Id, CheckinType.Manual, today, req.Note, req.Latitude, req.Longitude);
            if (req.MediaId.HasValue)
                checkin.MediaItems.Add(CheckinMedia.Create(checkin.Id, req.MediaId.Value, isPrimary: true));

            record.MarkCheckedIn(checkin.CheckedInAtUtc);

            await checkinRepo.AddAsync(checkin, ct);
            await checkinRepo.SaveChangesAsync(ct);
            return Result<CheckinDto>.Success(mapper.ToDto(checkin, req.MediaId));
        }

        // Nhánh B — Missed/Alerted: recovery checkin
        {
            var checkin = Checkin.Create(cmd.UserId, record.Id, CheckinType.Recovery, today, req.Note, req.Latitude, req.Longitude);
            if (req.MediaId.HasValue)
                checkin.MediaItems.Add(CheckinMedia.Create(checkin.Id, req.MediaId.Value, isPrimary: true));

            if (record.AlertIncident is not null
                && record.AlertIncident.Status != AlertIncidentStatus.Resolved)
                record.AlertIncident.Resolve();

            record.MarkResolved();

            await checkinRepo.AddAsync(checkin, ct);
            await checkinRepo.SaveChangesAsync(ct);
            return Result<CheckinDto>.Success(mapper.ToDto(checkin, req.MediaId));
        }
    }

    private async Task<DateTime> ComputeDeadlineAsync(Guid userId, DateOnly today, CancellationToken ct)
    {
        var setting = await safetySettingRepo.GetByUserIdAsync(userId, ct);
        var deadlineTime = setting?.DailyDeadlineLocalTime ?? new TimeOnly(23, 59);
        var deadlineVn = today.ToDateTime(deadlineTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(deadlineVn, VietnamTz);
    }
}
