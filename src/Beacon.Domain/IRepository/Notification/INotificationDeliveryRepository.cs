using Beacon.Domain.Entities.Notification;

namespace Beacon.Domain.IRepository.Notification;

public interface INotificationDeliveryRepository
{
    Task AddAsync(NotificationDelivery delivery, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<NotificationDelivery> deliveries, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
