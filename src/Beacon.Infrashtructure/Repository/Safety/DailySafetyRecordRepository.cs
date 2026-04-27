using Beacon.Domain.Entities.Safety;
using Beacon.Domain.IRepository.Safety;
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Safety;

public class DailySafetyRecordRepository(AppDbContext db) : IDailySafetyRecordRepository
{
    public Task<DailySafetyRecord?> GetByUserIdAndDateAsync(Guid userId, DateOnly date, CancellationToken ct = default)
        => db.DailySafetyRecords.FirstOrDefaultAsync(r => r.UserId == userId && r.Date == date, ct);

    public async Task AddAsync(DailySafetyRecord record, CancellationToken ct = default)
        => await db.DailySafetyRecords.AddAsync(record, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
