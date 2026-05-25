using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.IRepository.Safety;

namespace Beacon.Api.Backgroundjobs;

public class SafetyReminderJob(
    IDailySafetyRecordRepository repo,
    IFcmService fcm,
    ILogger<SafetyReminderJob> logger)
{
    public async Task ExecuteAsync()
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var records = await repo.GetPendingNeedingReminderAsync(now);

            foreach (var record in records)
            {
                try
                {
                    var remaining = (int)(record.DeadlineAtUtc - now.UtcDateTime).TotalMinutes;
                    await fcm.SendToUserAsync(
                        record.UserId,
                        "Nhắc nhở checkin an toàn",
                        $"Bạn còn {remaining} phút để checkin hôm nay.");

                    record.RecordReminderSent();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Reminder failed for UserId={UserId}", record.UserId);
                }
            }

            await repo.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SafetyReminderJob failed");
        }
    }
}
