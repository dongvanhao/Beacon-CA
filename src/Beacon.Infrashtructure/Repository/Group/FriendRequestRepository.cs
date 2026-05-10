using Beacon.Application.Common.Exceptions;
using Beacon.Domain.Entities.Group;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.IRepository.Group;
using Beacon.Infrashtructure.Presistence;
using Beacon.Shared.Common.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Group
{
    public class FriendRequestRepository(AppDbContext db) : IFriendRequestRepository
    {
        public Task<FriendRequest?> GetByIdAsync(Guid id, CancellationToken ct)
            => db.FriendRequests.Include(r => r.Initiator).FirstOrDefaultAsync(r => r.Id == id, ct);

        public Task<bool> HasPendingBetweenAsync(Guid userA, Guid userB, CancellationToken ct)
        {
            var (u1, u2) = userA < userB ? (userA, userB) : (userB, userA);
            return db.FriendRequests.AnyAsync(r =>
                r.Status == FriendRequestStatus.Pending &&
                r.UserId1 == u1 && r.UserId2 == u2, ct);
        }

        public Task<FriendRequest?> GetPendingBetweenAsync(Guid userA, Guid userB, CancellationToken ct)
        {
            var (u1, u2) = userA < userB ? (userA, userB) : (userB, userA);
            return db.FriendRequests.FirstOrDefaultAsync(r =>
                r.Status == FriendRequestStatus.Pending &&
                r.UserId1 == u1 && r.UserId2 == u2, ct);
        }

        public async Task<Dictionary<Guid, FriendRequest>> GetPendingBetweenBatchAsync(
            Guid userId, IEnumerable<Guid> targetIds, CancellationToken ct)
        {
            var targets = targetIds.ToList();
            var requests = await db.FriendRequests
                .Where(r => r.Status == FriendRequestStatus.Pending
                         && ((r.UserId1 == userId && targets.Contains(r.UserId2))
                          || (r.UserId2 == userId && targets.Contains(r.UserId1))))
                .ToListAsync(ct);

            // Key = the "other" user (not userId)
            return requests.ToDictionary(r => r.UserId1 == userId ? r.UserId2 : r.UserId1);
        }

        public async Task<CursorPagedResult<FriendRequest>> ListReceivedAsync(
            Guid receiverId, DateTime? cursor, int limit, CancellationToken ct)
        {
            var query = db.FriendRequests
                .AsNoTracking()
                .Include(r => r.Initiator).ThenInclude(u => u.AvatarMediaObject)
                .Where(r => r.Status == FriendRequestStatus.Pending && r.InitiatorId != receiverId
                         && (r.UserId1 == receiverId || r.UserId2 == receiverId))
                .OrderByDescending(r => r.CreatedAtUtc)
                .AsQueryable();

            if (cursor.HasValue)
                query = query.Where(r => r.CreatedAtUtc < cursor.Value);

            var items = await query.Take(limit + 1).ToListAsync(ct);
            var hasMore = items.Count > limit;
            if (hasMore) items.RemoveAt(items.Count - 1);

            return new CursorPagedResult<FriendRequest>
            {
                Data = items,
                Meta = new CursorMeta
                {
                    NextCursor = hasMore ? items[^1].CreatedAtUtc : null,
                    Limit = limit,
                    HasMore = hasMore
                }
            };
        }

        public async Task<CursorPagedResult<FriendRequest>> ListSentAsync(
            Guid senderId, DateTime? cursor, int limit, CancellationToken ct)
        {
            var query = db.FriendRequests
                .AsNoTracking()
                .Include(r => r.Initiator).ThenInclude(u => u.AvatarMediaObject)
                .Where(r => r.Status == FriendRequestStatus.Pending && r.InitiatorId == senderId)
                .OrderByDescending(r => r.CreatedAtUtc)
                .AsQueryable();

            if (cursor.HasValue)
                query = query.Where(r => r.CreatedAtUtc < cursor.Value);

            var items = await query.Take(limit + 1).ToListAsync(ct);
            var hasMore = items.Count > limit;
            if (hasMore) items.RemoveAt(items.Count - 1);

            return new CursorPagedResult<FriendRequest>
            {
                Data = items,
                Meta = new CursorMeta
                {
                    NextCursor = hasMore ? items[^1].CreatedAtUtc : null,
                    Limit = limit,
                    HasMore = hasMore
                }
            };
        }

        public async Task AddAsync(FriendRequest req, CancellationToken ct)
            => await db.FriendRequests.AddAsync(req, ct);

        public async Task SaveChangesAsync(CancellationToken ct)
        {
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException)
            {
                // FIX-05: another request modified the same FriendRequest row concurrently
                throw new ConflictException("Lời mời đã được xử lý bởi một yêu cầu khác.");
            }
        }
    }
}
