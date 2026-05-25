using Beacon.Domain.Entities.Group;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Group;
using Beacon.Infrashtructure.Presistence;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Group
{
    public class FriendRepository(AppDbContext db) : IFriendRepository
    {
        public Task<Friend?> GetByUsersAsync(Guid userA, Guid userB, CancellationToken ct)
        {
            var (u1, u2) = Beacon.Domain.Entities.Group.FriendPair.Normalize(userA, userB);
            return db.Friends
                .Include(f => f.User1).ThenInclude(u => u.AvatarMediaObject)
                .Include(f => f.User2).ThenInclude(u => u.AvatarMediaObject)
                .FirstOrDefaultAsync(f => f.UserId1 == u1 && f.UserId2 == u2, ct);
        }

        public Task<bool> AreFriendsAsync(Guid userA, Guid userB, CancellationToken ct)
        {
            var (u1, u2) = Beacon.Domain.Entities.Group.FriendPair.Normalize(userA, userB);
            return db.Friends
                .AnyAsync(f => f.UserId1 == u1 && f.UserId2 == u2, ct);
        }

        public async Task<HashSet<Guid>> GetFriendIdsAsync(Guid userId, IEnumerable<Guid> targetIds, CancellationToken ct)
        {
            var targets = targetIds.ToList();
            var list = await db.Friends
                .Where(f => (f.UserId1 == userId || f.UserId2 == userId)
                         && (targets.Contains(f.UserId1) || targets.Contains(f.UserId2)))
                .Select(f => f.UserId1 == userId ? f.UserId2 : f.UserId1)
                .ToListAsync(ct);
            return list.ToHashSet();
        }

        public async Task<List<Guid>> ListFriendIdsAsync(Guid userId, CancellationToken ct)
        {
            return await db.Friends
                .AsNoTracking()
                .Where(f => f.UserId1 == userId || f.UserId2 == userId)
                .Select(f => f.UserId1 == userId ? f.UserId2 : f.UserId1)
                .ToListAsync(ct);
        }

        public async Task<CursorPagedResult<FriendListItem>> ListByUserAsync(
            Guid userId, DateTime? cursor, int limit, CancellationToken ct)
        {
            var query = db.Friends
                .AsNoTracking()
                .Include(f => f.User1).ThenInclude(u => u.AvatarMediaObject)
                .Include(f => f.User2).ThenInclude(u => u.AvatarMediaObject)
                .Where(f => f.UserId1 == userId || f.UserId2 == userId)
                .OrderByDescending(f => f.CreatedAtUtc)
                .AsQueryable();

            if (cursor.HasValue)
                query = query.Where(f => f.CreatedAtUtc < cursor.Value);

            var friends = await query.Take(limit + 1).ToListAsync(ct);
            var hasMore = friends.Count > limit;
            if (hasMore) friends.RemoveAt(friends.Count - 1);

            var items = await AttachGroupIdsAsync(friends, ct);

            return new CursorPagedResult<FriendListItem>
            {
                Data = items,
                Meta = new CursorMeta
                {
                    NextCursor = hasMore ? friends[^1].CreatedAtUtc : null,
                    Limit = limit,
                    HasMore = hasMore
                }
            };
        }

        public async Task<CursorPagedResult<FriendListItem>> SearchByUserAsync(
            Guid userId, string search, DateTime? cursor, int limit, CancellationToken ct)
        {
            var normalizedSearch = StringNormalizer.RemoveDiacritics(search);
            var normalizedNoSpace = normalizedSearch.Replace(" ", "");
            var lowerSearch = search.Trim().ToLowerInvariant();

            var query = db.Friends
                .AsNoTracking()
                .Include(f => f.User1).ThenInclude(u => u!.AvatarMediaObject)
                .Include(f => f.User2).ThenInclude(u => u!.AvatarMediaObject)
                .Where(f =>
                    (f.UserId1 == userId || f.UserId2 == userId)
                    && (
                        (f.UserId1 == userId && (
                            f.User2.SearchIndex.Contains(normalizedSearch)
                            || f.User2.SearchIndex.Replace(" ", "").Contains(normalizedNoSpace)
                            || f.User2.Email.ToLower().Contains(lowerSearch)
                            || (f.User2.PhoneNumber != null && f.User2.PhoneNumber == search.Trim())))
                        ||
                        (f.UserId2 == userId && (
                            f.User1.SearchIndex.Contains(normalizedSearch)
                            || f.User1.SearchIndex.Replace(" ", "").Contains(normalizedNoSpace)
                            || f.User1.Email.ToLower().Contains(lowerSearch)
                            || (f.User1.PhoneNumber != null && f.User1.PhoneNumber == search.Trim())))
                    ))
                .OrderByDescending(f => f.CreatedAtUtc);

            var filteredQuery = cursor.HasValue
                ? query.Where(f => f.CreatedAtUtc < cursor.Value).OrderByDescending(f => f.CreatedAtUtc)
                : query;

            var friends = await filteredQuery.Take(limit + 1).ToListAsync(ct);
            var hasMore = friends.Count > limit;
            if (hasMore) friends.RemoveAt(friends.Count - 1);

            var items = await AttachGroupIdsAsync(friends, ct);

            return new CursorPagedResult<FriendListItem>
            {
                Data = items,
                Meta = new CursorMeta
                {
                    NextCursor = hasMore ? friends[^1].CreatedAtUtc : null,
                    Limit = limit,
                    HasMore = hasMore
                }
            };
        }

        public async Task AddAsync(Friend friend, CancellationToken ct)
            => await db.Friends.AddAsync(friend, ct);

        public async Task<bool> TryAddAsync(Friend friend, CancellationToken ct)
        {
            await db.Friends.AddAsync(friend, ct);
            try
            {
                await db.SaveChangesAsync(ct);
                return true;
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
                when (ex.InnerException?.Message.Contains("UNIQUE") == true
                   || ex.InnerException?.Message.Contains("unique") == true)
            {
                return false;
            }
        }

        public Task DeleteAsync(Friend friend, CancellationToken ct)
        {
            db.Friends.Remove(friend);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct)
            => db.SaveChangesAsync(ct);

        // Batch-load private group IDs for a list of friend pairs in 2 queries (no N+1).
        private async Task<List<FriendListItem>> AttachGroupIdsAsync(List<Friend> friends, CancellationToken ct)
        {
            if (friends.Count == 0) return [];

            var allUserIds = friends
                .SelectMany(f => new[] { f.UserId1, f.UserId2 })
                .Distinct()
                .ToList();

            // One query: all (GroupId, UserId) pairs for private groups that contain any relevant user
            var memberships = await db.MessageGroupMembers
                .Join(db.MessageGroups.Where(g => g.Type == MessageGroupType.Direct),
                    m => m.GroupId,
                    g => g.Id,
                    (m, _) => new { m.GroupId, m.UserId })
                .Where(x => allUserIds.Contains(x.UserId))
                .ToListAsync(ct);

            // Build per-user → group set map, then find the shared group for each pair
            var userGroups = memberships
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.GroupId).ToHashSet());

            return friends.Select(f =>
            {
                Guid? groupId = null;
                if (userGroups.TryGetValue(f.UserId1, out var g1) &&
                    userGroups.TryGetValue(f.UserId2, out var g2))
                {
                    groupId = g1.FirstOrDefault(id => g2.Contains(id));
                    if (groupId == Guid.Empty) groupId = null;
                }
                return new FriendListItem(f, groupId);
            }).ToList();
        }
    }
}
