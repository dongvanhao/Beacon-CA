using Beacon.Domain.Entities.User;

namespace Beacon.Application.Common.Interfaces.IRepository
{
    public interface IUserRefreshTokenRepository
    {
        Task<UserRefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserRefreshToken>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Task AddAsync(UserRefreshToken refreshToken, CancellationToken cancellationToken = default);
        void Update(UserRefreshToken refreshToken);
    }
}
