using Beacon.Domain.Entities.Identity;

namespace Beacon.Application.Common.Interfaces.IRepository
{
    public interface IAdminRepository
    {
        Task<Admin?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<Admin?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default);
        Task<bool> ExistsUserNameAsync(string userName, CancellationToken cancellationToken = default);
        Task AddAsync(Admin admin, CancellationToken cancellationToken = default);
        void Update(Admin admin);
    }
}
