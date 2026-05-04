using Beacon.Domain.Entities.Identity;

namespace Beacon.Domain.IRepository;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> ExistsByEmailExcludingUserAsync(string email, Guid excludeUserId, CancellationToken ct = default);
    Task<User?> GetByPhoneAsync(string phoneNumber, CancellationToken ct = default);
    Task<bool> ExistsByPhoneAsync(string phoneNumber, CancellationToken ct = default);
    Task<bool> ExistsByPhoneExcludingUserAsync(string phoneNumber, Guid excludeUserId, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task AddRefreshTokenAsync(RefreshToken token, CancellationToken ct = default);
    Task<RefreshToken?> GetActiveRefreshTokenAsync(string token, CancellationToken ct = default);
    Task<List<RefreshToken>> GetActiveRefreshTokensByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
