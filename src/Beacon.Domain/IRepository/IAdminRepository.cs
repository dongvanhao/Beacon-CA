using Beacon.Domain.Entities.Identity;

namespace Beacon.Domain.IRepository;

public interface IAdminRepository
{
    Task<Admin?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Admin?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<Admin?> GetByEmailWithRolesAsync(string email, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(Admin admin, CancellationToken ct = default);
    Task AddRefreshTokenAsync(RefreshTokenAdmin token, CancellationToken ct = default);
    Task<RefreshTokenAdmin?> GetActiveRefreshTokenAsync(string token, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
