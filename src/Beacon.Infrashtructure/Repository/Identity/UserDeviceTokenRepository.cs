using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository.Identity;
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Identity;

public class UserDeviceTokenRepository(AppDbContext db) : IUserDeviceTokenRepository
{
    public Task<UserDeviceToken?> GetByTokenAsync(string token, CancellationToken ct = default)
        => db.UserDeviceTokens.FirstOrDefaultAsync(t => t.Token == token, ct);

    public Task<List<UserDeviceToken>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default)
        => db.UserDeviceTokens
            .Where(t => t.UserId == userId && t.IsActive)
            .ToListAsync(ct);

    public async Task AddAsync(UserDeviceToken token, CancellationToken ct = default)
        => await db.UserDeviceTokens.AddAsync(token, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
