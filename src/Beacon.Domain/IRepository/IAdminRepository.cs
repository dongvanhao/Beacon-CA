using Beacon.Domain.Entities.Identity;
using Beacon.Shared.Common.Pagination;

namespace Beacon.Domain.IRepository;

public interface IAdminRepository
{
    Task<PaginatedList<Admin>> ListAsync(string? search, int page, int pageSize, CancellationToken ct = default);
    Task<(int Total, int Active, int Inactive)> CountStatusAsync(CancellationToken ct = default);
    Task<Admin?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Admin?> GetByIdWithRolesAsync(Guid id, CancellationToken ct = default);
    Task<Admin?> GetByIdWithRolesNoTrackingAsync(Guid id, CancellationToken ct = default);
    Task<Admin?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<Admin?> GetByUsernameWithRolesAsync(string username, CancellationToken ct = default);
    Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default);
    Task<bool> ExistsByUsernameAsync(string username, Guid excludeId, CancellationToken ct = default);
    Task AddAsync(Admin admin, CancellationToken ct = default);
    Task AddRefreshTokenAsync(RefreshTokenAdmin token, CancellationToken ct = default);
    Task<RefreshTokenAdmin?> GetActiveRefreshTokenAsync(string token, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
