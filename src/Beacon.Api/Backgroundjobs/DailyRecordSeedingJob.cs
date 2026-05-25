using Beacon.Domain.Entities.Safety;
using Beacon.Domain.IRepository.Safety;
using Beacon.Domain.IRepository.Settings;

namespace Beacon.Api.Backgroundjobs;

public class DailyRecordSeedingJob(
    ISafetySettingRepository settingRepo,
    IDailySafetyRecordRepository recordRepo,
    ILogger<DailyRecordSeedingJob> logger)
{
    private static readonly TimeZoneInfo VietnamTz = GetVietnamTimeZone();

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
    }

    public async Task ExecuteAsync()
    {
        try
        {
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTz));
            var settings = await settingRepo.GetActiveMonitoringUsersWithoutRecordAsync(today);

            foreach (var setting in settings)
            {
                var deadlineVn  = today.ToDateTime(setting.DailyDeadlineLocalTime, DateTimeKind.Unspecified);
                var deadlineUtc = TimeZoneInfo.ConvertTimeToUtc(deadlineVn, VietnamTz);
                var record      = DailySafetyRecord.Create(setting.UserId, today, deadlineUtc);
                await recordRepo.AddAsync(record);
            }

            if (settings.Count > 0)
            {
                await recordRepo.SaveChangesAsync();
                logger.LogInformation("DailyRecordSeedingJob seeded {Count} record(s) for {Date}", settings.Count, today);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DailyRecordSeedingJob failed");
        }
    }
}
