using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Infrashtructure.Presistence;
using Beacon.Shared.Common.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Messaging;

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
    {
        var directKey = MessageGroup.BuildDirectKey(userId1, userId2);
        return db.MessageGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.DirectKey == directKey, ct);
    }

    public Task<MessageGroup?> GetByDirectKeyAsync(string directKey, CancellationToken ct)
        => db.MessageGroups.FirstOrDefaultAsync(g => g.DirectKey == directKey, ct);

    public Task<MessageGroup?> GetByDirectKeyIncludingDeletedAsync(string directKey, CancellationToken ct)
        => db.MessageGroups
            .IgnoreQueryFilters()
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.DirectKey == directKey, ct);

    public Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct)
        => db.MessageGroupMembers
            .AnyAsync(m => m.GroupId == groupId
                && m.UserId == userId
                && m.Status == MessageGroupMemberStatus.Joined, ct);

    public Task<List<Guid>> GetGroupIdsByUserAsync(Guid userId, CancellationToken ct)
        => db.MessageGroupMembers
            .Where(m => m.UserId == userId && m.Status == MessageGroupMemberStatus.Joined)
            .Select(m => m.GroupId)
            .ToListAsync(ct);

    public async Task<CursorPagedResult<MessageGroupSummary>> ListByUserAsync(
        Guid userId, DateTime? cursor, int limit, CancellationToken ct)
    {
        var query = db.MessageGroupMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.Status == MessageGroupMemberStatus.Joined)
            .Join(db.MessageGroups,
                m => m.GroupId,
                g => g.Id,
                (m, g) => new { Member = m, Group = g })
            .Select(x => new
            {
                x.Group.Id,
                x.Group.Type,
                x.Group.DirectKey,
                x.Group.CreatedAtUtc,
                x.Group.RequireApprovalToAddMembers,
                x.Group.Name,
                GroupAvatarObjectKey = x.Group.AvatarMedia != null ? x.Group.AvatarMedia.ObjectKey : null,
                LastMessageId = x.Group.Messages
                    .OrderByDescending(m => m.SequenceNumber)
                    .Select(m => (Guid?)m.Id)
                    .FirstOrDefault(),
                LastMessageContent = x.Group.Messages
                    .OrderByDescending(m => m.SequenceNumber)
                    .Select(m => m.Content)
                    .FirstOrDefault(),
                LastMessageAtUtc = x.Group.Messages
                    .OrderByDescending(m => m.SequenceNumber)
                    .Select(m => (DateTime?)m.CreatedAtUtc)
                    .FirstOrDefault(),
                LastMessageSenderFamilyName = x.Group.Messages
                    .OrderByDescending(m => m.SequenceNumber)
                    .Select(m => m.Sender.FamilyName)
                    .FirstOrDefault(),
                LastMessageSenderGivenName = x.Group.Messages
                    .OrderByDescending(m => m.SequenceNumber)
                    .Select(m => m.Sender.GivenName)
                    .FirstOrDefault(),
                x.Member.LastSeenMessageId,
                UnreadCount = x.Member.LastSeenMessageId == null
                    ? x.Group.Messages.Count()
                    : x.Group.Messages.Count(m =>
                        m.SequenceNumber > x.Group.Messages
                            .Where(seen => seen.Id == x.Member.LastSeenMessageId)
                            .Select(seen => (long?)seen.SequenceNumber)
                            .FirstOrDefault()),
                PeerDisplayName = x.Group.Type == MessageGroupType.Direct
                    ? x.Group.Members
                        .Where(m => m.UserId != userId)
                        .Where(m => m.Status == MessageGroupMemberStatus.Joined)
                        .Select(m => (m.User.FamilyName + " " + m.User.GivenName).Trim())
                        .FirstOrDefault()
                    : null,
                PeerUserId = x.Group.Type == MessageGroupType.Direct
                    ? x.Group.Members
                        .Where(m => m.UserId != userId)
                        .Where(m => m.Status == MessageGroupMemberStatus.Joined)
                        .Select(m => (Guid?)m.UserId)
                        .FirstOrDefault()
                    : null,
                PeerAvatarObjectKey = x.Group.Type == MessageGroupType.Direct
                    ? x.Group.Members
                        .Where(m => m.UserId != userId)
                        .Where(m => m.Status == MessageGroupMemberStatus.Joined)
                        .Select(m => m.User.AvatarMediaObject != null ? m.User.AvatarMediaObject.ObjectKey : null)
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

        var groupIdsNeedingFallbackName = rawItems
            .Where(x => x.Type == MessageGroupType.Group && string.IsNullOrWhiteSpace(x.Name))
            .Select(x => x.Id)
            .Distinct()
            .ToList();

        var fallbackNameMap = new Dictionary<Guid, string>();
        if (groupIdsNeedingFallbackName.Count > 0)
        {
            var memberNames = await db.MessageGroupMembers
                .AsNoTracking()
                .Where(m => groupIdsNeedingFallbackName.Contains(m.GroupId)
                    && m.Status == MessageGroupMemberStatus.Joined)
                .OrderBy(m => m.JoinedAtUtc)
                .Select(m => new
                {
                    m.GroupId,
                    Name = (m.User.FamilyName + " " + m.User.GivenName).Trim()
                })
                .ToListAsync(ct);

            fallbackNameMap = memberNames
                .GroupBy(x => x.GroupId)
                .ToDictionary(
                    g => g.Key,
                    g => string.Join(", ", g.Select(x => x.Name).Where(n => n != string.Empty).Take(3)));
        }

        var items = rawItems
            .Select(x => new MessageGroupSummary(
                x.Id, x.Type, x.DirectKey, x.PeerUserId, x.CreatedAtUtc, x.RequireApprovalToAddMembers,
                x.LastMessageId,
                x.LastMessageContent, x.LastMessageAtUtc,
                x.LastMessageSenderFamilyName, x.LastMessageSenderGivenName,
                x.LastSeenMessageId,
                IsSeenLatest: x.LastMessageId is null || x.LastSeenMessageId == x.LastMessageId,
                x.UnreadCount,
                DisplayName: ResolveDisplayName(x.Type, x.Name, x.PeerDisplayName,
                    fallbackNameMap.GetValueOrDefault(x.Id)),
                AvatarObjectKey: x.Type == MessageGroupType.Direct
                    ? x.PeerAvatarObjectKey
                    : x.GroupAvatarObjectKey))
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

    private static string ResolveDisplayName(
        MessageGroupType type,
        string? groupName,
        string? peerDisplayName,
        string? groupFallbackName)
    {
        if (type == MessageGroupType.Direct)
            return !string.IsNullOrWhiteSpace(peerDisplayName) ? peerDisplayName : "Người dùng";

        if (!string.IsNullOrWhiteSpace(groupName))
            return groupName;

        return !string.IsNullOrWhiteSpace(groupFallbackName) ? groupFallbackName : "Nhóm chat";
    }
}
