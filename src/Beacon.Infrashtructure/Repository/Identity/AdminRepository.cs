using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository;
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Identity;

public class AdminRepository(AppDbContext context) : IAdminRepository
{
    public async Task<Admin?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Admins.FirstOrDefaultAsync(a => a.Id == id, ct);

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
