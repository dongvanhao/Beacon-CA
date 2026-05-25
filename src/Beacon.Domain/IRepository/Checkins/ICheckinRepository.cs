using Beacon.Domain.Entities.Checkins;
using Beacon.Shared.Common.Pagination;

namespace Beacon.Domain.IRepository.Checkins;

public interface ICheckinRepository
{
    Task AddAsync(Checkin checkin, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    Task<int> GetStreakAsync(Guid userId, DateOnly today, CancellationToken ct = default);
    Task<CursorPagedResult<Checkin>> GetPagedByUserIdAsync(
        Guid userId, DateTimeOffset? cursor, int limit, CancellationToken ct = default);
}
