using Beacon.Domain.Entities.Identity;
using Beacon.Shared.Common.Pagination;

namespace Beacon.Domain.IRepository.Identity;

public interface IRoleRepository
{
    Task<PaginatedList<Role>> ListWithPermissionsAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Role>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<Role?> GetByIdWithPermissionsAsync(Guid id, CancellationToken ct = default);
    Task<Role?> GetByIdWithPermissionsNoTrackingAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken ct = default);
    Task<bool> IsAssignedToAnyAdminAsync(Guid roleId, CancellationToken ct = default);
    Task<bool> HasPermissionAsync(Guid roleId, Guid permissionId, CancellationToken ct = default);
    Task<RolePermission?> GetRolePermissionAsync(Guid roleId, Guid permissionId, CancellationToken ct = default);
    Task<bool> HasAdminRoleAsync(Guid adminId, Guid roleId, CancellationToken ct = default);
    Task<IReadOnlyList<AdminRole>> ListAdminRolesByAdminIdAsync(Guid adminId, CancellationToken ct = default);
    Task AddAsync(Role role, CancellationToken ct = default);
    Task AddRolePermissionAsync(RolePermission rolePermission, CancellationToken ct = default);
    Task AddAdminRoleAsync(AdminRole adminRole, CancellationToken ct = default);
    void RemoveAdminRole(AdminRole adminRole);
    void RemoveRolePermission(RolePermission rolePermission);
    void Remove(Role role);
    Task SaveChangesAsync(CancellationToken ct = default);
}
