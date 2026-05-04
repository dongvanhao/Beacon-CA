using Beacon.Domain.Entities.Messaging;
using Beacon.Shared.Common.Pagination;

namespace Beacon.Domain.IRepository.Messaging
{
    public interface IMessageRepository
    {
        Task<CursorPagedResult<Message>> ListByGroupAsync(Guid groupId, DateTime? cursor, int limit, CancellationToken ct);
        Task AddAsync(Message message, CancellationToken ct);
        Task SaveChangesAsync(CancellationToken ct);
    }
}
