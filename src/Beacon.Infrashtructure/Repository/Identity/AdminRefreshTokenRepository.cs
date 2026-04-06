using Beacon.Application.Common.Interfaces.IRepository;
using Beacon.Domain.Entities.Identity;
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Identity
{
    public class AdminRefreshTokenRepository : IAdminRefreshTokenRepository
    {
        private readonly AppDbContext _dbContext;

        public AdminRefreshTokenRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<RefreshTokenAdmin?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            return _dbContext.RefreshTokenAdmins
                .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        }

        public async Task<IReadOnlyList<RefreshTokenAdmin>> GetByAdminIdAsync(int adminId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.RefreshTokenAdmins
                .Where(x => x.AdminId == adminId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(RefreshTokenAdmin refreshToken, CancellationToken cancellationToken = default)
        {
            await _dbContext.RefreshTokenAdmins.AddAsync(refreshToken, cancellationToken);
        }

        public void Update(RefreshTokenAdmin refreshToken)
        {
            _dbContext.RefreshTokenAdmins.Update(refreshToken);
        }
    }
}
