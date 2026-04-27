using Beacon.Domain.Entities.Setting;

namespace Beacon.Domain.IRepository.Settings;

public interface ISafetySettingRepository
{
    Task<SafetySetting?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(SafetySetting setting, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
