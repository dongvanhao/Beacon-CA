using Beacon.Domain.Entities.Group;
using Beacon.Shared.Common.Pagination;

namespace Beacon.Domain.IRepository.Group
{
    public interface IFriendRepository
    {
        Task<Friend?> GetByUsersAsync(Guid userId1, Guid userId2, CancellationToken ct);
        Task<bool> AreFriendsAsync(Guid userA, Guid userB, CancellationToken ct);
        Task<CursorPagedResult<Friend>> ListByUserAsync(Guid userId, DateTime? cursor, int limit, CancellationToken ct);
        Task AddAsync(Friend friend, CancellationToken ct);
        Task DeleteAsync(Friend friend, CancellationToken ct);
        Task SaveChangesAsync(CancellationToken ct);
    }
}
