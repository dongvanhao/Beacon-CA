using Beacon.Domain.Entities.Checkins;
using Beacon.Domain.IRepository.Checkins;
using Beacon.Infrashtructure.Presistence;

namespace Beacon.Infrashtructure.Repository.Checkins;

public class CheckinRepository(AppDbContext db) : ICheckinRepository
{
    public async Task AddAsync(Checkin checkin, CancellationToken ct = default)
        => await db.Checkins.AddAsync(checkin, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
