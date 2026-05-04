using Beacon.Domain.Entities.Messaging;
using Beacon.Shared.Common.Pagination;

namespace Beacon.Domain.IRepository.Messaging
{
    public record MessageGroupSummary(
        Guid GroupId,
        bool IsPrivate,
        DateTime CreatedAtUtc,
        string? LastMessageContent,
        DateTime? LastMessageAtUtc,
        string? LastMessageSenderFamilyName,
        string? LastMessageSenderGivenName,
        string? PeerFamilyName,
        string? PeerGivenName);

    public interface IMessageGroupRepository
    {
        Task<MessageGroup?> GetByIdAsync(Guid id, CancellationToken ct);
        Task<MessageGroup?> GetByIdWithMembersAsync(Guid id, CancellationToken ct);
        Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct);
        Task<CursorPagedResult<MessageGroupSummary>> ListByUserAsync(Guid userId, DateTime? cursor, int limit, CancellationToken ct);
        Task AddAsync(MessageGroup group, CancellationToken ct);
        Task RemoveMembersAsync(Guid groupId, Guid userId1, Guid userId2, CancellationToken ct);
        Task SaveChangesAsync(CancellationToken ct);
    }
}
