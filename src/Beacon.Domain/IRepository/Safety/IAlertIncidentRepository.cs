using Beacon.Domain.Entities.Safety;

namespace Beacon.Domain.IRepository.Safety;

public interface IAlertIncidentRepository
{
    Task AddAsync(AlertIncident incident, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
