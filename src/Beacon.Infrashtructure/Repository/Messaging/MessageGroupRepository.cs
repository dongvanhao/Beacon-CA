using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Infrashtructure.Presistence;
using Beacon.Shared.Common.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Messaging
{
    public class MessageGroupRepository(AppDbContext db) : IMessageGroupRepository
    {
        public Task<MessageGroup?> GetByIdAsync(Guid id, CancellationToken ct)
            => db.MessageGroups
                .Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Id == id, ct);

        public Task<MessageGroup?> GetByIdWithMembersAsync(Guid id, CancellationToken ct)
            => db.MessageGroups
                .Include(g => g.AvatarMedia)
                .Include(g => g.Members)
                    .ThenInclude(m => m.User)
                        .ThenInclude(u => u.AvatarMediaObject)
                .FirstOrDefaultAsync(g => g.Id == id, ct);

        public Task<MessageGroup?> GetPrivateGroupBetweenAsync(Guid userId1, Guid userId2, CancellationToken ct)
            => db.MessageGroups
                .Include(g => g.Members)
                .Where(g => g.IsPrivate
                    && g.Members.Any(m => m.UserId == userId1)
                    && g.Members.Any(m => m.UserId == userId2))
                .FirstOrDefaultAsync(ct);

        public Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct)
            => db.MessageGroupMembers
                .AnyAsync(m => m.GroupId == groupId && m.UserId == userId, ct);

        public Task<List<Guid>> GetGroupIdsByUserAsync(Guid userId, CancellationToken ct)
            => db.MessageGroupMembers
                .Where(m => m.UserId == userId)
                .Select(m => m.GroupId)
                .ToListAsync(ct);

        public async Task<CursorPagedResult<MessageGroupSummary>> ListByUserAsync(
            Guid userId, DateTime? cursor, int limit, CancellationToken ct)
        {
            // Project into anonymous type — EF Core can translate OrderBy/Where on anonymous
            // type properties without recreating the object in SQL (unlike named record types).
            var query = db.MessageGroupMembers
                .AsNoTracking()
                .Where(m => m.UserId == userId)
                .Join(db.MessageGroups,
                    m => m.GroupId,
                    g => g.Id,
                    (m, g) => g)
                .Select(g => new
                {
                    g.Id,
                    g.IsPrivate,
                    g.CreatedAtUtc,
                    g.Name,
                    AvatarObjectKey = g.AvatarMedia != null ? g.AvatarMedia.ObjectKey : null,
                    LastMessageContent = g.Messages
                        .OrderByDescending(m => m.CreatedAtUtc)
                        .Select(m => m.Content)
                        .FirstOrDefault(),
                    LastMessageAtUtc = g.Messages
                        .OrderByDescending(m => m.CreatedAtUtc)
                        .Select(m => (DateTime?)m.CreatedAtUtc)
                        .FirstOrDefault(),
                    LastMessageSenderFamilyName = g.Messages
                        .OrderByDescending(m => m.CreatedAtUtc)
                        .Select(m => m.Sender.FamilyName)
                        .FirstOrDefault(),
                    LastMessageSenderGivenName = g.Messages
                        .OrderByDescending(m => m.CreatedAtUtc)
                        .Select(m => m.Sender.GivenName)
                        .FirstOrDefault(),
                    // Tên hiển thị: custom name nếu có, fallback = "FamilyName GivenName" của peer (chat 1-1)
                    PeerDisplayName = g.IsPrivate
                        ? g.Members
                            .Where(m => m.UserId != userId)
                            .Select(m => (m.User.FamilyName + " " + m.User.GivenName).Trim())
                            .FirstOrDefault()
                        : null
                });

            if (cursor.HasValue)
                query = query.Where(x => (x.LastMessageAtUtc ?? x.CreatedAtUtc) < cursor.Value);

            var rawItems = await query
                .OrderByDescending(x => x.LastMessageAtUtc ?? x.CreatedAtUtc)
                .Take(limit + 1)
                .ToListAsync(ct);

            var hasMore = rawItems.Count > limit;
            if (hasMore) rawItems.RemoveAt(rawItems.Count - 1);

            var items = rawItems
                .Select(x => new MessageGroupSummary(
                    x.Id, x.IsPrivate, x.CreatedAtUtc,
                    x.LastMessageContent, x.LastMessageAtUtc,
                    x.LastMessageSenderFamilyName, x.LastMessageSenderGivenName,
                    DisplayName: x.Name ?? x.PeerDisplayName,
                    AvatarObjectKey: x.AvatarObjectKey))
                .ToList();

            return new CursorPagedResult<MessageGroupSummary>
            {
                Data = items,
                Meta = new CursorMeta
                {
                    NextCursor = hasMore
                        ? (items[^1].LastMessageAtUtc ?? items[^1].CreatedAtUtc)
                        : null,
                    Limit = limit,
                    HasMore = hasMore
                }
            };
        }

        public async Task AddAsync(MessageGroup group, CancellationToken ct)
            => await db.MessageGroups.AddAsync(group, ct);

        public async Task RemoveMemberAsync(Guid groupId, Guid userId, CancellationToken ct)
        {
            var member = await db.MessageGroupMembers
                .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId, ct);
            if (member is not null)
                db.MessageGroupMembers.Remove(member);
        }

        public Task SaveChangesAsync(CancellationToken ct)
            => db.SaveChangesAsync(ct);
    }
}
