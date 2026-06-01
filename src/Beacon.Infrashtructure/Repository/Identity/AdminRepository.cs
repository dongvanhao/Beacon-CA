using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository;
using Beacon.Infrashtructure.Presistence;
using Beacon.Shared.Common.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Identity;

public class AdminRepository(AppDbContext context) : IAdminRepository
{
    public Task<PaginatedList<Admin>> ListAsync(string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.Admins
            .AsNoTracking()
            .Include(a => a.AdminRoles)
                .ThenInclude(ar => ar.Role)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLower();
            query = query.Where(a =>
                a.Username.ToLower().Contains(keyword) ||
                a.FullName.ToLower().Contains(keyword));
        }

        query = query
            .OrderByDescending(a => a.CreatedAtUtc)
            .ThenBy(a => a.Username);

        return PaginatedList<Admin>.CreateAsync(query, page, pageSize, ct);
    }

    public async Task<(int Total, int Active, int Inactive)> CountStatusAsync(CancellationToken ct = default)
    {
        var total = await context.Admins.CountAsync(ct);
        var active = await context.Admins.CountAsync(a => a.IsActive, ct);
        return (total, active, total - active);
    }

    public async Task<Admin?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Admins.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<Admin?> GetByIdWithRolesAsync(Guid id, CancellationToken ct = default)
        => await context.Admins
            .Include(a => a.AdminRoles)
                .ThenInclude(ar => ar.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<Admin?> GetByIdWithRolesNoTrackingAsync(Guid id, CancellationToken ct = default)
        => await context.Admins
            .AsNoTracking()
            .Include(a => a.AdminRoles)
                .ThenInclude(ar => ar.Role)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<Admin?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => await context.Admins
            .FirstOrDefaultAsync(a => a.Username == username.ToLowerInvariant(), ct);

    public async Task<Admin?> GetByUsernameWithRolesAsync(string username, CancellationToken ct = default)
        => await context.Admins
            .Include(a => a.AdminRoles)
                .ThenInclude(ar => ar.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(a => a.Username == username.ToLowerInvariant(), ct);

    public async Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default)
        => await context.Admins.AnyAsync(a => a.Username == username.ToLowerInvariant(), ct);

    public async Task<bool> ExistsByUsernameAsync(string username, Guid excludeId, CancellationToken ct = default)
        => await context.Admins.AnyAsync(
            a => a.Username == username.ToLowerInvariant()
                && a.Id != excludeId,
            ct);

    public async Task AddAsync(Admin admin, CancellationToken ct = default)
        => await context.Admins.AddAsync(admin, ct);

    public async Task AddRefreshTokenAsync(RefreshTokenAdmin token, CancellationToken ct = default)
        => await context.RefreshTokenAdmins.AddAsync(token, ct);

    public async Task<RefreshTokenAdmin?> GetActiveRefreshTokenAsync(string token, CancellationToken ct = default)
        => await context.RefreshTokenAdmins
            .FirstOrDefaultAsync(rt => rt.Token == token
                && rt.RevokedAtUtc == null
                && rt.ExpiresAtUtc > DateTime.UtcNow, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
