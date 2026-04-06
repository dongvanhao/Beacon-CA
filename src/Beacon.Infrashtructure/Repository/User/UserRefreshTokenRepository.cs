using Beacon.Application.Common.Interfaces.IRepository;
using Beacon.Domain.Entities.User;
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.User
{
    public class UserRefreshTokenRepository : IUserRefreshTokenRepository
    {
        private readonly AppDbContext _dbContext;

        public UserRefreshTokenRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<UserRefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            return _dbContext.RefreshTokens
                .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        }

        public async Task<IReadOnlyList<UserRefreshToken>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.RefreshTokens
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(UserRefreshToken refreshToken, CancellationToken cancellationToken = default)
        {
            await _dbContext.RefreshTokens.AddAsync(refreshToken, cancellationToken);
        }

        public void Update(UserRefreshToken refreshToken)
        {
            _dbContext.RefreshTokens.Update(refreshToken);
        }
    }
}
