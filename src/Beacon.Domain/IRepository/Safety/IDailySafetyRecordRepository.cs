using Beacon.Domain.Entities.Safety;

namespace Beacon.Domain.IRepository.Safety;

public interface IDailySafetyRecordRepository
{
    Task<DailySafetyRecord?> GetByUserIdAndDateAsync(Guid userId, DateOnly date, CancellationToken ct = default);
    Task<DailySafetyRecord?> GetByUserIdAndDateWithIncidentAsync(Guid userId, DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<DailySafetyRecord>> GetPendingNeedingReminderAsync(DateTimeOffset now, CancellationToken ct = default);
    Task<IReadOnlyList<DailySafetyRecord>> GetPendingPastDeadlineAsync(DateTimeOffset now, CancellationToken ct = default);
    Task<IReadOnlyList<DailySafetyRecord>> GetMissedNeedingAlertAsync(DateTimeOffset now, CancellationToken ct = default);
    Task AddAsync(DailySafetyRecord record, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
