using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.Entities.Group;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.IRepository.Group;
using Beacon.Domain.IRepository.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Beacon.Infrashtructure.Services;

public class NotificationService(
    INotificationRepository notifRepo,
    IRealtimeNotifier notifier,
    IServiceScopeFactory scopeFactory,
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
        // Step 1: persist — must succeed before any delivery
        var notification = Notification.Create(receiverUserId, type, title, body, data);
        await notifRepo.AddAsync(notification, ct);
        await notifRepo.SaveChangesAsync(ct);

        var payload = new NotificationPayload(notification.Id, type.ToString(), title, body, data);

        // Step 2: SignalR — online clients receive it immediately, failure is non-fatal
        try
        {
            await notifier.NotifyUserAsync(receiverUserId, payload, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SignalR emit failed for user {UserId}", receiverUserId);
        }

        // Step 3: FCM — fire-and-forget with a dedicated scope so scoped services (FcmService,
        // IUserDeviceTokenRepository) are not accessed after the request scope is disposed.
        var notificationId = notification.Id;
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope   = scopeFactory.CreateAsyncScope();
                var fcm       = scope.ServiceProvider.GetRequiredService<IFcmService>();
                var tokenRepo = scope.ServiceProvider.GetRequiredService<IUserDeviceTokenRepository>();

                var fcmData      = BuildFcmData(type, notificationId, data);
                var invalidTokens = await fcm.SendToUserAndGetInvalidTokensAsync(
                    receiverUserId, title, body, fcmData);

                foreach (var token in invalidTokens)
                {
                    var t = await tokenRepo.GetByTokenAsync(token);
                    t?.MarkInvalid();
                }
                if (invalidTokens.Count > 0)
                    await tokenRepo.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "FCM delivery failed for user {UserId}", receiverUserId);
            }
        }, CancellationToken.None);
    }

    private static Dictionary<string, string> BuildFcmData(
        NotificationType type, Guid notificationId, string? extraData)
    {
        var d = new Dictionary<string, string>
        {
            ["type"] = "NOTIFICATION",
            ["notificationType"] = type.ToString(),
            ["notificationId"] = notificationId.ToString()
        };
        if (!string.IsNullOrEmpty(extraData))
            d["data"] = extraData;
        return d;
    }
}
