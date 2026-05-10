using Beacon.Domain.Entities.Identity;

namespace Beacon.Domain.IRepository.Identity;

public interface IUserDeviceTokenRepository
{
    Task<UserDeviceToken?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task<List<UserDeviceToken>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(UserDeviceToken token, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
