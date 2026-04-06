using DomainUser = Beacon.Domain.Entities.User.User;

namespace Beacon.Application.Common.Interfaces.IRepository
{
    public interface IUserRepository
    {
        Task<DomainUser?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<DomainUser?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default);
        Task<DomainUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<DomainUser?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default);
        Task<bool> ExistsUserNameAsync(string userName, CancellationToken cancellationToken = default);
        Task<bool> ExistsEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<bool> ExistsPhoneAsync(string phone, CancellationToken cancellationToken = default);
        Task AddAsync(DomainUser user, CancellationToken cancellationToken = default);
        void Update(DomainUser user);
    }
}
