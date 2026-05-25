using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.Entities.Safety;
using Beacon.Domain.Enums.Checkins;
using Beacon.Domain.IRepository.Safety;

namespace Beacon.Api.Backgroundjobs;

public class SafetyMissedCheckerJob(
    IDailySafetyRecordRepository recordRepo,
    IAlertIncidentRepository alertRepo,
    IFcmService fcm,
    ILogger<SafetyMissedCheckerJob> logger)
{
    public async Task ExecuteAsync()
    {
        var now = DateTimeOffset.UtcNow;

        // Phase 1 — Mark Missed
        try
        {
            var pendingPastDeadline = await recordRepo.GetPendingPastDeadlineAsync(now);
            foreach (var record in pendingPastDeadline)
                record.MarkMissed();
            await recordRepo.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SafetyMissedCheckerJob Phase 1 (MarkMissed) failed");
        }

        // Phase 2 — Create AlertIncident + FCM
        try
        {
            var missedNeedingAlert = await recordRepo.GetMissedNeedingAlertAsync(now);
            foreach (var record in missedNeedingAlert)
            {
                try
                {
                    var incident = AlertIncident.Create(
                        record.UserId, record.Id, AlertIncidentType.MissedCheckin);

                    if (!fcm.IsAvailable)
                    {
                        // Firebase credential not configured — mark Failed so ops can diagnose;
                        // record is still MarkAlerted to prevent infinite retry loops.
                        incident.MarkFailed("FCM not configured");
                        logger.LogWarning("FCM unavailable — incident marked Failed for UserId={UserId}", record.UserId);
                    }
                    else
                    {
                        await fcm.SendToUserAsync(
                            record.UserId,
                            "Cảnh báo: Bạn chưa checkin!",
                            "Hệ thống đã ghi nhận bạn chưa checkin hôm nay. Vui lòng checkin ngay.");
                        incident.MarkSent();
                    }

                    record.MarkAlerted();
                    await alertRepo.AddAsync(incident);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Alert failed for UserId={UserId}", record.UserId);
                }
            }
            await alertRepo.SaveChangesAsync();
            await recordRepo.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SafetyMissedCheckerJob Phase 2 (AlertIncident) failed");
        }
    }
}
