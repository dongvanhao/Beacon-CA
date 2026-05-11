using Beacon.Domain.Entities.Group;
using Beacon.Shared.Common.Pagination;

namespace Beacon.Domain.IRepository.Group
{
    /// <summary>Friend entity + the ID of its associated private MessageGroup (looked up from MessageGroupMembers).</summary>
    public record FriendListItem(Friend Friend, Guid? MessageGroupId);

    public interface IFriendRepository
    {
        Task<Friend?> GetByUsersAsync(Guid userId1, Guid userId2, CancellationToken ct);
        Task<bool> AreFriendsAsync(Guid userA, Guid userB, CancellationToken ct);
        Task<HashSet<Guid>> GetFriendIdsAsync(Guid userId, IEnumerable<Guid> targetIds, CancellationToken ct);
        Task<List<Guid>> ListFriendIdsAsync(Guid userId, CancellationToken ct);
        Task<CursorPagedResult<FriendListItem>> ListByUserAsync(Guid userId, DateTime? cursor, int limit, CancellationToken ct);
        Task<CursorPagedResult<FriendListItem>> SearchByUserAsync(Guid userId, string search, DateTime? cursor, int limit, CancellationToken ct);
        Task AddAsync(Friend friend, CancellationToken ct);
        Task<bool> TryAddAsync(Friend friend, CancellationToken ct);
        Task DeleteAsync(Friend friend, CancellationToken ct);
        Task SaveChangesAsync(CancellationToken ct);
    }
}
