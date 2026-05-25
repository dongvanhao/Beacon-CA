using Beacon.Domain.Entities.Safety;

namespace Beacon.Domain.IRepository.Safety;

public interface IEmergencyContactRepository
{
    Task<IReadOnlyList<EmergencyContact>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<EmergencyContact>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<EmergencyContact?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<EmergencyContact?> GetPrimaryByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<int> CountActiveByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(EmergencyContact contact, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
