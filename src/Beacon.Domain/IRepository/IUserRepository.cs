using Beacon.Domain.Entities.Identity;

namespace Beacon.Domain.IRepository;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task AddRefreshTokenAsync(RefreshToken token, CancellationToken ct = default);
    Task<RefreshToken?> GetActiveRefreshTokenAsync(string token, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
