using Beacon.Domain.Entities.Checkins;

namespace Beacon.Domain.IRepository.Checkins;

public interface ICheckinRepository
{
    Task AddAsync(Checkin checkin, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
