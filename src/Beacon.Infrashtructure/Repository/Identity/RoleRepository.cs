using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository.Identity;
using Beacon.Infrashtructure.Presistence;
using Beacon.Shared.Common.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Identity;

public class RoleRepository(AppDbContext db) : IRoleRepository
{
    public Task<PaginatedList<Role>> ListWithPermissionsAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = db.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLower();
            query = query.Where(r =>
                r.Name.ToLower().Contains(keyword) ||
                (r.Description != null && r.Description.ToLower().Contains(keyword)));
        }

        query = query.OrderBy(r => r.Name);

        return PaginatedList<Role>.CreateAsync(query, page, pageSize, ct);
    }

    public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<Role?> GetByIdWithPermissionsAsync(Guid id, CancellationToken ct = default)
        => db.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<Role?> GetByIdWithPermissionsNoTrackingAsync(Guid id, CancellationToken ct = default)
        => db.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken ct = default)
        => db.Roles.AnyAsync(
            r => r.Name == name && (!excludeId.HasValue || r.Id != excludeId.Value),
            ct);

    public Task<bool> IsAssignedToAnyAdminAsync(Guid roleId, CancellationToken ct = default)
        => db.AdminRoles.AnyAsync(ar => ar.RoleId == roleId, ct);

    public Task<bool> HasPermissionAsync(Guid roleId, Guid permissionId, CancellationToken ct = default)
        => db.RolePermissions.AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId, ct);

    public Task<RolePermission?> GetRolePermissionAsync(Guid roleId, Guid permissionId, CancellationToken ct = default)
        => db.RolePermissions.FirstOrDefaultAsync(
            rp => rp.RoleId == roleId && rp.PermissionId == permissionId,
            ct);

    public Task<bool> HasAdminRoleAsync(Guid adminId, Guid roleId, CancellationToken ct = default)
        => db.AdminRoles.AnyAsync(ar => ar.AdminId == adminId && ar.RoleId == roleId, ct);

    public async Task AddAsync(Role role, CancellationToken ct = default)
        => await db.Roles.AddAsync(role, ct);

    public async Task AddRolePermissionAsync(RolePermission rolePermission, CancellationToken ct = default)
        => await db.RolePermissions.AddAsync(rolePermission, ct);

    public async Task AddAdminRoleAsync(AdminRole adminRole, CancellationToken ct = default)
        => await db.AdminRoles.AddAsync(adminRole, ct);

    public void RemoveRolePermission(RolePermission rolePermission)
        => db.RolePermissions.Remove(rolePermission);

    public void Remove(Role role)
        => db.Roles.Remove(role);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
