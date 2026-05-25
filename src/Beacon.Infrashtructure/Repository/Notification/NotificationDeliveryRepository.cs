using Beacon.Domain.Entities.Notification;
using Beacon.Domain.IRepository.Notification;
using Beacon.Infrashtructure.Presistence;

namespace Beacon.Infrashtructure.Repository.Notification;

public class NotificationDeliveryRepository(AppDbContext db) : INotificationDeliveryRepository
{
    public Task AddAsync(NotificationDelivery delivery, CancellationToken ct = default)
    {
        db.NotificationDeliveries.Add(delivery);
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(IEnumerable<NotificationDelivery> deliveries, CancellationToken ct = default)
    {
        db.NotificationDeliveries.AddRange(deliveries);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
