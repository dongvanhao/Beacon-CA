using Beacon.Domain.Entities.Group;
using Beacon.Domain.IRepository.Group;
using Beacon.Infrashtructure.Presistence;
using Beacon.Shared.Common.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Group
{
    public class FriendRepository(AppDbContext db) : IFriendRepository
    {
        public Task<Friend?> GetByUsersAsync(Guid userA, Guid userB, CancellationToken ct)
        {
            var (u1, u2) = userA < userB ? (userA, userB) : (userB, userA);
            return db.Friends
                .Include(f => f.User1)
                .Include(f => f.User2)
                .FirstOrDefaultAsync(f => f.UserId1 == u1 && f.UserId2 == u2, ct);
        }

        public Task<bool> AreFriendsAsync(Guid userA, Guid userB, CancellationToken ct)
        {
            var (u1, u2) = userA < userB ? (userA, userB) : (userB, userA);
            return db.Friends
                .AnyAsync(f => f.UserId1 == u1 && f.UserId2 == u2, ct);
        }

        public async Task<CursorPagedResult<Friend>> ListByUserAsync(
            Guid userId, DateTime? cursor, int limit, CancellationToken ct)
        {
            var query = db.Friends
                .AsNoTracking()
                .Include(f => f.User1)
                .Include(f => f.User2)
                .Where(f => f.UserId1 == userId || f.UserId2 == userId)
                .OrderByDescending(f => f.CreatedAtUtc)
                .AsQueryable();

            if (cursor.HasValue)
                query = query.Where(f => f.CreatedAtUtc < cursor.Value);

            var items = await query.Take(limit + 1).ToListAsync(ct);
            var hasMore = items.Count > limit;
            if (hasMore) items.RemoveAt(items.Count - 1);

            return new CursorPagedResult<Friend>
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

        public async Task AddAsync(Friend friend, CancellationToken ct)
            => await db.Friends.AddAsync(friend, ct);

        public Task DeleteAsync(Friend friend, CancellationToken ct)
        {
            db.Friends.Remove(friend);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct)
            => db.SaveChangesAsync(ct);
    }
}
