using Beacon.Domain.Entities.Safety;
using Beacon.Domain.Enums.Safety;
using Beacon.Domain.IRepository.Safety;
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Safety;

public class DailySafetyRecordRepository(AppDbContext db) : IDailySafetyRecordRepository
{
    public Task<DailySafetyRecord?> GetByUserIdAndDateAsync(Guid userId, DateOnly date, CancellationToken ct = default)
        => db.DailySafetyRecords.FirstOrDefaultAsync(r => r.UserId == userId && r.Date == date, ct);

    public Task<DailySafetyRecord?> GetByUserIdAndDateWithIncidentAsync(Guid userId, DateOnly date, CancellationToken ct = default)
        => db.DailySafetyRecords
            .Include(r => r.AlertIncident)
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Date == date, ct);

    public async Task<IReadOnlyList<DailySafetyRecord>> GetPendingNeedingReminderAsync(
        DateTimeOffset now, CancellationToken ct = default)
        => await db.DailySafetyRecords
            .Join(db.SafetySettings, r => r.UserId, s => s.UserId, (r, s) => new { r, s })
            .Where(x => x.r.Status == SafetyStatus.Pending
                     && x.r.ReminderSentAtUtc == null
                     && x.s.IsMonitoringEnabled
                     && x.r.DeadlineAtUtc <= now.UtcDateTime.AddMinutes(x.s.ReminderBeforeMinutes))
            .Select(x => x.r)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DailySafetyRecord>> GetPendingPastDeadlineAsync(
        DateTimeOffset now, CancellationToken ct = default)
        => await db.DailySafetyRecords
            .Join(db.SafetySettings, r => r.UserId, s => s.UserId, (r, s) => new { r, s })
            .Where(x => x.r.Status == SafetyStatus.Pending
                     && x.r.DeadlineAtUtc < now.UtcDateTime
                     && x.s.IsMonitoringEnabled)
            .Select(x => x.r)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DailySafetyRecord>> GetMissedNeedingAlertAsync(
        DateTimeOffset now, CancellationToken ct = default)
        => await db.DailySafetyRecords
            .Join(db.SafetySettings, r => r.UserId, s => s.UserId, (r, s) => new { r, s })
            .Where(x => (x.r.Status == SafetyStatus.Missed || x.r.Status == SafetyStatus.Alerted)
                     && !db.AlertIncidents.Any(a => a.DailySafetyRecordId == x.r.Id)
                     && x.s.IsAutoAlertEnabled
                     && x.r.MarkedMissedAtUtc != null
                     && x.r.MarkedMissedAtUtc.Value.AddMinutes(x.s.AutoAlertDelayMinutes) <= now.UtcDateTime)
            .Select(x => x.r)
            .ToListAsync(ct);

    public async Task AddAsync(DailySafetyRecord record, CancellationToken ct = default)
        => await db.DailySafetyRecords.AddAsync(record, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
