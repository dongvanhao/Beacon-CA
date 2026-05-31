using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository.Identity;
using Beacon.Infrashtructure.Presistence;
using Beacon.Shared.Common.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Identity;

public class PermissionRepository(AppDbContext db) : IPermissionRepository
{
    public Task<PaginatedList<Permission>> ListAsync(
        string? search,
        string? group,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = db.Permissions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(keyword) ||
                (p.Description != null && p.Description.ToLower().Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(group))
        {
            var normalizedGroup = group.Trim().ToLower();
            query = query.Where(p =>
                p.Group != null && p.Group.ToLower() == normalizedGroup);
        }

        query = query
            .OrderBy(p => p.Group)
            .ThenBy(p => p.Name);

        return PaginatedList<Permission>.CreateAsync(query, page, pageSize, ct);
    }

    public async Task<IReadOnlyList<string>> ListGroupsAsync(CancellationToken ct = default)
        => await db.Permissions
            .AsNoTracking()
            .Where(p => p.Group != null && p.Group != string.Empty)
            .Select(p => p.Group!)
            .Distinct()
            .OrderBy(group => group)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Permission>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => await db.Permissions
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .OrderBy(p => p.Group)
            .ThenBy(p => p.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Permission>> GetByNamesAsync(IReadOnlyCollection<string> names, CancellationToken ct = default)
        => await db.Permissions
            .AsNoTracking()
            .Where(p => names.Contains(p.Name))
            .OrderBy(p => p.Group)
            .ThenBy(p => p.Name)
            .ToListAsync(ct);

    public Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Permissions.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Permission?> GetByNameAsync(string name, CancellationToken ct = default)
        => db.Permissions.FirstOrDefaultAsync(p => p.Name == name, ct);

    public Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken ct = default)
        => db.Permissions.AnyAsync(
            p => p.Name == name && (!excludeId.HasValue || p.Id != excludeId.Value),
            ct);

    public Task<bool> IsAssignedToAnyRoleAsync(Guid permissionId, CancellationToken ct = default)
        => db.RolePermissions.AnyAsync(rp => rp.PermissionId == permissionId, ct);

    public async Task AddAsync(Permission permission, CancellationToken ct = default)
        => await db.Permissions.AddAsync(permission, ct);

    public void Remove(Permission permission)
        => db.Permissions.Remove(permission);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
