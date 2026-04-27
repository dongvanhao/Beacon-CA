using Beacon.Domain.Entities.Checkins;
using Beacon.Domain.IRepository.Checkins;
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Checkins;

public class CheckinRepository(AppDbContext db) : ICheckinRepository
{
    public async Task AddAsync(Checkin checkin, CancellationToken ct = default)
        => await db.Checkins.AddAsync(checkin, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);

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
