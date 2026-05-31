using Beacon.Domain.Entities.Identity;

namespace Beacon.Domain.IRepository;

public interface IAdminRepository
{
    Task<Admin?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Admin?> GetByIdWithRolesAsync(Guid id, CancellationToken ct = default);
    Task<Admin?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<Admin?> GetByUsernameWithRolesAsync(string username, CancellationToken ct = default);
    Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default);
    Task AddAsync(Admin admin, CancellationToken ct = default);
    Task AddRefreshTokenAsync(RefreshTokenAdmin token, CancellationToken ct = default);
    Task<RefreshTokenAdmin?> GetActiveRefreshTokenAsync(string token, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
