using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository;
using Beacon.Infrashtructure.Presistence;
using Beacon.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Identity;

public class UserRepository(AppDbContext context) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => await context.Users.FirstOrDefaultAsync(u => u.Username == username.ToLowerInvariant(), ct);

    public async Task<bool> ExistsAsync(Guid userId, CancellationToken ct = default)
        => await context.Users.AnyAsync(u => u.Id == userId, ct);

    public async Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default)
        => await context.Users.AnyAsync(u => u.Username == username.ToLowerInvariant(), ct);

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
        => await context.Users.AnyAsync(u => u.Email == email.ToLowerInvariant(), ct);

    public async Task<bool> ExistsByEmailExcludingUserAsync(string email, Guid excludeUserId, CancellationToken ct = default)
        => await context.Users.AnyAsync(u => u.Email == email.Trim().ToLowerInvariant() && u.Id != excludeUserId, ct);

    public async Task<User?> GetByPhoneAsync(string phoneNumber, CancellationToken ct = default)
        => await context.Users
            .Include(u => u.AvatarMediaObject)
            .FirstOrDefaultAsync(u => u.PhoneNumber != null && u.PhoneNumber == phoneNumber, ct);

    public async Task<List<User>> SearchByNameOrPhoneAsync(string keyword, Guid excludeUserId, int limit, CancellationToken ct = default)
    {
        // Normalize keyword: bỏ dấu để hỗ trợ tìm kiếm có dấu / không dấu như nhau
        var normalizedKeyword = StringNormalizer.RemoveDiacritics(keyword);

        // Bỏ thêm khoảng trắng để hỗ trợ tìm "DongHao" khớp với SearchIndex "dong hao"
        var normalizedNoSpace = normalizedKeyword.Replace(" ", "");

        var lowerKeyword = keyword.Trim().ToLowerInvariant();

        return await context.Users
            .Include(u => u.AvatarMediaObject)
            .Where(u => u.Id != excludeUserId && (
                u.SearchIndex.Contains(normalizedKeyword) ||                    // "Dong Hao" → "dong hao" ✓
                u.SearchIndex.Replace(" ", "").Contains(normalizedNoSpace) ||   // "DongHao"  → "donghao" ✓
                u.Username.ToLower().Contains(lowerKeyword) ||                  // username
                u.Email.ToLower().Contains(lowerKeyword) ||                     // email
                (u.PhoneNumber != null && u.PhoneNumber == keyword.Trim())))    // phone (exact)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsByPhoneAsync(string phoneNumber, CancellationToken ct = default)
        => await context.Users.AnyAsync(u => u.PhoneNumber != null && u.PhoneNumber == phoneNumber, ct);

    public async Task<bool> ExistsByPhoneExcludingUserAsync(string phoneNumber, Guid excludeUserId, CancellationToken ct = default)
        => await context.Users.AnyAsync(u => u.PhoneNumber == phoneNumber.Trim() && u.Id != excludeUserId, ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await context.Users.AddAsync(user, ct);

    public async Task AddRefreshTokenAsync(RefreshToken token, CancellationToken ct = default)
        => await context.RefreshTokens.AddAsync(token, ct);

    public async Task<RefreshToken?> GetActiveRefreshTokenAsync(string token, CancellationToken ct = default)
        => await context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == token
                && rt.RevokedAtUtc == null
                && rt.ExpiresAtUtc > DateTime.UtcNow, ct);

    public async Task<List<RefreshToken>> GetActiveRefreshTokensByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await context.RefreshTokens
            .Where(rt => rt.UserId == userId
                && rt.RevokedAtUtc == null
                && rt.ExpiresAtUtc > DateTime.UtcNow)
            .ToListAsync(ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
