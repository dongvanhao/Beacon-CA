using Beacon.Domain.Entities.Identity;
using Beacon.Shared.Common.Pagination;

namespace Beacon.Domain.IRepository.Identity;

public interface IPermissionRepository
{
    Task<PaginatedList<Permission>> ListAsync(
        string? search,
        string? group,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListGroupsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Permission>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<IReadOnlyList<Permission>> GetByNamesAsync(IReadOnlyCollection<string> names, CancellationToken ct = default);
    Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Permission?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken ct = default);
    Task<bool> IsAssignedToAnyRoleAsync(Guid permissionId, CancellationToken ct = default);
    Task AddAsync(Permission permission, CancellationToken ct = default);
    void Remove(Permission permission);
    Task SaveChangesAsync(CancellationToken ct = default);
}
