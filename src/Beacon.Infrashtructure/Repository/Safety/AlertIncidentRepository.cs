using Beacon.Domain.Entities.Safety;
using Beacon.Domain.IRepository.Safety;
using Beacon.Infrashtructure.Presistence;

namespace Beacon.Infrashtructure.Repository.Safety;

public class AlertIncidentRepository(AppDbContext db) : IAlertIncidentRepository
{
    public async Task AddAsync(AlertIncident incident, CancellationToken ct = default)
        => await db.AlertIncidents.AddAsync(incident, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
