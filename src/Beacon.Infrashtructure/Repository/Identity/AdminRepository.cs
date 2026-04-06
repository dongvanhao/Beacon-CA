using Beacon.Application.Common.Interfaces.IRepository;
using Beacon.Domain.Entities.Identity;
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Identity
{
    public class AdminRepository : IAdminRepository
    {
        private readonly AppDbContext _dbContext;

        public AdminRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<Admin?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return _dbContext.Admins
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }

        public Task<Admin?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default)
        {
            return _dbContext.Admins
                .FirstOrDefaultAsync(x => x.UserName == userName, cancellationToken);
        }

        public Task<bool> ExistsUserNameAsync(string userName, CancellationToken cancellationToken = default)
        {
            return _dbContext.Admins
                .AnyAsync(x => x.UserName == userName, cancellationToken);
        }

        public async Task AddAsync(Admin admin, CancellationToken cancellationToken = default)
        {
            await _dbContext.Admins.AddAsync(admin, cancellationToken);
        }

        public void Update(Admin admin)
        {
            _dbContext.Admins.Update(admin);
        }
    }
}
