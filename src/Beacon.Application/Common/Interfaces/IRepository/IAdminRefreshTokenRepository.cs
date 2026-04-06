using Beacon.Domain.Entities.Identity;

namespace Beacon.Application.Common.Interfaces.IRepository
{
    public interface IAdminRefreshTokenRepository
    {
        Task<RefreshTokenAdmin?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<RefreshTokenAdmin>> GetByAdminIdAsync(int adminId, CancellationToken cancellationToken = default);
        Task AddAsync(RefreshTokenAdmin refreshToken, CancellationToken cancellationToken = default);
        void Update(RefreshTokenAdmin refreshToken);
    }
}
