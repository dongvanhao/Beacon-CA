using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository;
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Identity;

public class UserRepository(AppDbContext context) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await context.Users.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), ct);

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
        => await context.Users.AnyAsync(u => u.Email == email.ToLowerInvariant(), ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await context.Users.AddAsync(user, ct);

    public async Task AddRefreshTokenAsync(RefreshToken token, CancellationToken ct = default)
        => await context.RefreshTokens.AddAsync(token, ct);

    public async Task<RefreshToken?> GetActiveRefreshTokenAsync(string token, CancellationToken ct = default)
        => await context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == token
                && rt.RevokedAtUtc == null
                && rt.ExpiresAtUtc > DateTime.UtcNow, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
