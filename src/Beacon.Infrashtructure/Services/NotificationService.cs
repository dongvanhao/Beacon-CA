using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.Entities.Group;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.IRepository.Group;
using Microsoft.Extensions.Logging;

namespace Beacon.Infrashtructure.Services;

public class NotificationService(
    INotificationRepository notifRepo,
    IRealtimeNotifier notifier,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task CreateAndDeliverAsync(
        Guid receiverUserId,
        NotificationType type,
        string title,
        string body,
        string? data = null,
        CancellationToken ct = default)
    {
        var notification = Notification.Create(receiverUserId, type, title, body, data);
        await notifRepo.AddAsync(notification, ct);
        await notifRepo.SaveChangesAsync(ct);

        var payload = new NotificationPayload(notification.Id, type.ToString(), title, body, data);
        try
        {
            await notifier.NotifyUserAsync(receiverUserId, payload, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SignalR emit failed for user {UserId}", receiverUserId);
        }
    }
}
