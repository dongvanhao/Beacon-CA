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
            => db.FriendRequests.FirstOrDefaultAsync(r => r.Id == id, ct);

        public Task<bool> HasPendingBetweenAsync(Guid userA, Guid userB, CancellationToken ct)
            => db.FriendRequests.AnyAsync(r =>
                r.Status == FriendRequestStatus.Pending &&
                ((r.SenderId == userA && r.ReceiverId == userB) ||
                 (r.SenderId == userB && r.ReceiverId == userA)), ct);

        public async Task<CursorPagedResult<FriendRequest>> ListReceivedAsync(
            Guid receiverId, DateTime? cursor, int limit, CancellationToken ct)
        {
            var query = db.FriendRequests
                .AsNoTracking()
                .Include(r => r.Sender)
                .Where(r => r.ReceiverId == receiverId && r.Status == FriendRequestStatus.Pending)
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
                .Where(r => r.SenderId == senderId && r.Status == FriendRequestStatus.Pending)
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

        public Task SaveChangesAsync(CancellationToken ct)
            => db.SaveChangesAsync(ct);
    }
}
