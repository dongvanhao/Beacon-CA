using Beacon.Domain.Entities.Identity;

namespace Beacon.Domain.IRepository;

public interface IUserDeviceRepository
{
    Task<UserDevice?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserDevice?> GetByDeviceTokenAsync(Guid userId, string deviceToken, CancellationToken ct = default);
    Task AddAsync(UserDevice device, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
