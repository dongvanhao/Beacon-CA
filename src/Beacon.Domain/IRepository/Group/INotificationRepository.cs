using Beacon.Domain.Entities.Group;

namespace Beacon.Domain.IRepository.Group;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Notification>> ListByReceiverAsync(Guid receiverUserId, DateTime? cursor, int limit, CancellationToken ct = default);
    Task<int> CountUnreadAsync(Guid receiverUserId, CancellationToken ct = default);
    Task AddAsync(Notification notification, CancellationToken ct = default);
    Task MarkAllReadAsync(Guid receiverUserId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
