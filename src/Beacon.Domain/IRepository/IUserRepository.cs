using Beacon.Domain.Entities.Identity;
using Beacon.Shared.Common.Pagination;

namespace Beacon.Domain.IRepository;

public interface IUserRepository
{
    Task<PaginatedList<User>> ListAsync(string? search, int page, int pageSize, CancellationToken ct = default);
    Task<(int Total, int Active, int Inactive)> CountStatusAsync(CancellationToken ct = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid userId, CancellationToken ct = default);
    Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default);
    Task<bool> ExistsByUsernameAsync(string username, Guid excludeId, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> ExistsByEmailExcludingUserAsync(string email, Guid excludeUserId, CancellationToken ct = default);
    Task<User?> GetByPhoneAsync(string phoneNumber, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<List<User>> SearchByNameOrPhoneAsync(string keyword, Guid excludeUserId, int limit, CancellationToken ct = default);
    Task<bool> ExistsByPhoneAsync(string phoneNumber, CancellationToken ct = default);
    Task<bool> ExistsByPhoneExcludingUserAsync(string phoneNumber, Guid excludeUserId, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task AddRefreshTokenAsync(RefreshToken token, CancellationToken ct = default);
    Task<RefreshToken?> GetActiveRefreshTokenAsync(string token, CancellationToken ct = default);
    Task<List<RefreshToken>> GetActiveRefreshTokensByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
