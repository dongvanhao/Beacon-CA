using Beacon.Domain.Entities.Setting;
using Beacon.Domain.IRepository.Settings;
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Settings;

public class SafetySettingRepository(AppDbContext db) : ISafetySettingRepository
{
    public Task<SafetySetting?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => db.SafetySettings.FirstOrDefaultAsync(x => x.UserId == userId, ct);

    public async Task<IReadOnlyList<SafetySetting>> GetActiveMonitoringUsersWithoutRecordAsync(
        DateOnly today, CancellationToken ct = default)
        => await db.SafetySettings
            .Where(s => s.IsMonitoringEnabled
                     && !db.DailySafetyRecords.Any(r => r.UserId == s.UserId && r.Date == today))
            .ToListAsync(ct);

    public async Task AddAsync(SafetySetting setting, CancellationToken ct = default)
        => await db.SafetySettings.AddAsync(setting, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
