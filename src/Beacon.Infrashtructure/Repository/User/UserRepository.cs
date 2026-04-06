using Beacon.Application.Common.Interfaces.IRepository;
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;
using DomainUser = Beacon.Domain.Entities.User.User;

namespace Beacon.Infrashtructure.Repository.User
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _dbContext;

        public UserRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<DomainUser?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return _dbContext.Users
                .Include(x => x.UserSetting)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }

        public Task<DomainUser?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default)
        {
            return _dbContext.Users
                .FirstOrDefaultAsync(x => x.UserName == userName, cancellationToken);
        }

        public Task<DomainUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return _dbContext.Users
                .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        }

        public Task<DomainUser?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default)
        {
            return _dbContext.Users
                .FirstOrDefaultAsync(x => x.Phone == phone, cancellationToken);
        }

        public Task<bool> ExistsUserNameAsync(string userName, CancellationToken cancellationToken = default)
        {
            return _dbContext.Users
                .AnyAsync(x => x.UserName == userName, cancellationToken);
        }

        public Task<bool> ExistsEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return _dbContext.Users
                .AnyAsync(x => x.Email == email, cancellationToken);
        }

        public Task<bool> ExistsPhoneAsync(string phone, CancellationToken cancellationToken = default)
        {
            return _dbContext.Users
                .AnyAsync(x => x.Phone == phone, cancellationToken);
        }

        public async Task AddAsync(DomainUser user, CancellationToken cancellationToken = default)
        {
            await _dbContext.Users.AddAsync(user, cancellationToken);
        }

        public void Update(DomainUser user)
        {
            _dbContext.Users.Update(user);
        }
    }
}
