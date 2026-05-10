using Beacon.Domain.Entities.Messaging;
using Beacon.Shared.Common.Pagination;

namespace Beacon.Domain.IRepository.Messaging
{
    public interface IMessageRepository
    {
        Task<CursorPagedResult<Message, long>> ListByGroupAsync(Guid groupId, long? cursor, int limit, CancellationToken ct);
        Task<Message?> GetByClientMessageIdAsync(Guid groupId, string clientMessageId, CancellationToken ct);
        Task<bool> ExistsInGroupAsync(Guid groupId, Guid messageId, CancellationToken ct);
        Task AddAsync(Message message, CancellationToken ct);
        Task SaveChangesAsync(CancellationToken ct);
    }
}
