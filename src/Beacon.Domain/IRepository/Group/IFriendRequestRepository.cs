using Beacon.Domain.Entities.Group;
using Beacon.Shared.Common.Pagination;

namespace Beacon.Domain.IRepository.Group
{
    public interface IFriendRequestRepository
    {
        Task<FriendRequest?> GetByIdAsync(Guid id, CancellationToken ct);
        Task<bool> HasPendingBetweenAsync(Guid userA, Guid userB, CancellationToken ct);
        Task<FriendRequest?> GetPendingBetweenAsync(Guid userA, Guid userB, CancellationToken ct);
        Task<Dictionary<Guid, FriendRequest>> GetPendingBetweenBatchAsync(Guid userId, IEnumerable<Guid> targetIds, CancellationToken ct);
        Task<CursorPagedResult<FriendRequest>> ListReceivedAsync(Guid receiverId, DateTime? cursor, int limit, CancellationToken ct);
        Task<CursorPagedResult<FriendRequest>> ListSentAsync(Guid senderId, DateTime? cursor, int limit, CancellationToken ct);
        Task AddAsync(FriendRequest req, CancellationToken ct);
        Task SaveChangesAsync(CancellationToken ct);
    }
}
