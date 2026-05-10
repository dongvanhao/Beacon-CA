using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.Entities.Group;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.IRepository.Group;
using Beacon.Domain.IRepository.Identity;
using Microsoft.Extensions.Logging;

namespace Beacon.Infrashtructure.Services;

public class NotificationService(
    INotificationRepository notifRepo,
    IRealtimeNotifier notifier,
    IFcmService fcmService,
    IUserDeviceTokenRepository tokenRepo,
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

        // Step 3: FCM — fire-and-forget so the request is not blocked or cancelled
        _ = Task.Run(async () =>
        {
            try
            {
                var fcmData = BuildFcmData(type, notification.Id, data);
                var invalidTokens = await fcmService.SendToUserAndGetInvalidTokensAsync(
                    receiverUserId, title, body, fcmData);

                if (invalidTokens.Count > 0)
                    await MarkTokensInvalidAsync(invalidTokens);
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

    private async Task MarkTokensInvalidAsync(IReadOnlyList<string> tokens)
    {
        foreach (var token in tokens)
        {
            var t = await tokenRepo.GetByTokenAsync(token);
            t?.MarkInvalid();
        }
        await tokenRepo.SaveChangesAsync();
    }
}
