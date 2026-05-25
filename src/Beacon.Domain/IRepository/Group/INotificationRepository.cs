using Beacon.Domain.Entities.Group;
using NotificationEntity = Beacon.Domain.Entities.Group.Notification;

namespace Beacon.Domain.IRepository.Group;

public interface INotificationRepository
{
    Task<NotificationEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<NotificationEntity>> ListByReceiverAsync(Guid receiverUserId, DateTime? cursor, int limit, CancellationToken ct = default);
    Task<int> CountUnreadAsync(Guid receiverUserId, CancellationToken ct = default);
    Task AddAsync(NotificationEntity notification, CancellationToken ct = default);
    Task MarkAllReadAsync(Guid receiverUserId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
