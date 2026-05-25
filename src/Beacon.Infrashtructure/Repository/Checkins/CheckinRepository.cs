using Beacon.Domain.Entities.Checkins;
using Beacon.Domain.IRepository.Checkins;
using Beacon.Infrashtructure.Presistence;
using Beacon.Shared.Common.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Checkins;

public class CheckinRepository(AppDbContext db) : ICheckinRepository
{
    public async Task AddAsync(Checkin checkin, CancellationToken ct = default)
        => await db.Checkins.AddAsync(checkin, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);

    public async Task<CursorPagedResult<Checkin>> GetPagedByUserIdAsync(
        Guid userId, DateTimeOffset? cursor, int limit, CancellationToken ct = default)
    {
        var query = db.Checkins
            .Include(c => c.MediaItems)
                .ThenInclude(m => m.MediaObject)
            .Where(c => c.UserId == userId);

        if (cursor.HasValue)
            query = query.Where(c => c.CheckedInAtUtc < cursor.Value.UtcDateTime);

        var items = await query
            .OrderByDescending(c => c.CheckedInAtUtc)
            .Take(limit + 1)
            .ToListAsync(ct);

        var hasMore = items.Count > limit;
        if (hasMore) items.RemoveAt(limit);

        return new CursorPagedResult<Checkin>
        {
            Data = items,
            Meta = new CursorMeta
            {
                NextCursor = hasMore ? items.Last().CheckedInAtUtc : (DateTime?)null,
                Limit = limit,
                HasMore = hasMore
            }
        };
    }

    public async Task<int> GetStreakAsync(Guid userId, DateOnly today, CancellationToken ct = default)
    {
        var cutoff = today.AddDays(-365);

        var checkinDates = await db.Checkins
            .Where(c => c.UserId == userId
                     && c.CheckinDate >= cutoff
                     && c.CheckinDate <= today)
            .Select(c => c.CheckinDate)
            .Distinct()
            .OrderByDescending(d => d)
            .ToListAsync(ct);

        if (checkinDates.Count == 0)
            return 0;

        // Chuỗi phải bắt đầu từ hôm nay hoặc hôm qua
        if (checkinDates[0] < today.AddDays(-1))
            return 0;

        int streak = 0;
        var expected = checkinDates[0];

        foreach (var date in checkinDates)
        {
            if (date != expected) break;
            streak++;
            expected = expected.AddDays(-1);
        }

        return streak;
    }
}
