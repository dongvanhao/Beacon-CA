using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Enums.Messaging;
using Beacon.Shared.Common.Pagination;

namespace Beacon.Domain.IRepository.Messaging;

public record MessageGroupSummary(
    Guid GroupId,
    MessageGroupType Type,
    string? DirectKey,
    DateTime CreatedAtUtc,
    string? LastMessageContent,
    DateTime? LastMessageAtUtc,
    string? LastMessageSenderFamilyName,
    string? LastMessageSenderGivenName,
    string? DisplayName,
    string? AvatarObjectKey);

public interface IMessageGroupRepository
{
    Task<MessageGroup?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<MessageGroup?> GetByIdWithMembersAsync(Guid id, CancellationToken ct);
    Task<MessageGroup?> GetPrivateGroupBetweenAsync(Guid userId1, Guid userId2, CancellationToken ct);
    Task<MessageGroup?> GetByDirectKeyAsync(string directKey, CancellationToken ct);
    Task<MessageGroup?> GetByDirectKeyIncludingDeletedAsync(string directKey, CancellationToken ct);
    Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct);
    Task<List<Guid>> GetGroupIdsByUserAsync(Guid userId, CancellationToken ct);
    Task<CursorPagedResult<MessageGroupSummary>> ListByUserAsync(Guid userId, DateTime? cursor, int limit, CancellationToken ct);
    Task AddAsync(MessageGroup group, CancellationToken ct);
    Task RemoveMemberAsync(Guid groupId, Guid userId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
