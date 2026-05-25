using System.Text.Json;
using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Notification;
using Beacon.Domain.Entities.Safety;
using Beacon.Domain.Enums;
using Beacon.Domain.Enums.Checkins;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.Enums.Notification;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Notification;
using Beacon.Domain.IRepository.Safety;
using Hangfire;

namespace Beacon.Api.Backgroundjobs;

[DisableConcurrentExecution(timeoutInSeconds: 600)]
public class SafetyMissedCheckerJob(
    IDailySafetyRecordRepository recordRepo,
    IAlertIncidentRepository alertRepo,
    IFcmService fcm,
    IEmergencyContactRepository emergencyContactRepo,
    INotificationDeliveryRepository notifDeliveryRepo,
    IUserRepository userRepo,
    INotificationService notificationService,
    ILogger<SafetyMissedCheckerJob> logger)
{
    public async Task ExecuteAsync()
    {
        var now = DateTimeOffset.UtcNow;

        // Phase 1 — Mark Missed
        try
        {
            var pendingPastDeadline = await recordRepo.GetPendingPastDeadlineAsync(now);
            foreach (var record in pendingPastDeadline)
                record.MarkMissed();
            await recordRepo.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SafetyMissedCheckerJob Phase 1 (MarkMissed) failed");
            throw;
        }

        // Phase 2 — Create AlertIncident + FCM to victim
        var alertedPairs = new List<(AlertIncident incident, DailySafetyRecord record)>();
        try
        {
            var missedNeedingAlert = await recordRepo.GetMissedNeedingAlertAsync(now);
            foreach (var record in missedNeedingAlert)
            {
                try
                {
                    var incident = AlertIncident.Create(
                        record.UserId, record.Id, AlertIncidentType.MissedCheckin);

                    if (!fcm.IsAvailable)
                    {
                        incident.MarkFailed("FCM not configured");
                        logger.LogWarning("FCM unavailable — incident marked Failed for UserId={UserId}", record.UserId);
                    }
                    else
                    {
                        var sent = await fcm.SendToUserAsync(
                            record.UserId,
                            "Cảnh báo: Bạn chưa checkin!",
                            "Hệ thống đã ghi nhận bạn chưa checkin hôm nay. Vui lòng checkin ngay.");
                        if (sent)
                            incident.MarkSent();
                        else
                        {
                            incident.MarkFailed("No active device tokens");
                            logger.LogWarning("No active device tokens — incident marked Failed for UserId={UserId}", record.UserId);
                        }
                    }

                    record.MarkAlerted();
                    await alertRepo.AddAsync(incident);
                    alertedPairs.Add((incident, record));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Alert failed for UserId={UserId}", record.UserId);
                }
            }
            await alertRepo.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SafetyMissedCheckerJob Phase 2 (AlertIncident) failed");
            throw;
        }

        // Phase 3 — Notify emergency contacts via in-app notification + FCM
        await ExecutePhase3Async(alertedPairs, default);
    }

    private async Task ExecutePhase3Async(
        List<(AlertIncident incident, DailySafetyRecord record)> pairs,
        CancellationToken ct)
    {
        if (!fcm.IsAvailable)
        {
            logger.LogWarning("FCM unavailable — Phase 3 skipped entirely");
            return;
        }

        try
        {
            foreach (var (incident, record) in pairs)
            {
                var activeContacts = await emergencyContactRepo.GetActiveByUserIdAsync(record.UserId, ct);
                if (activeContacts.Count == 0) continue;

                var victim = await userRepo.GetByIdAsync(record.UserId, ct);
                var victimName = victim?.Username ?? record.UserId.ToString();

                var title = $"Cảnh báo khẩn: {victimName} chưa checkin!";
                var body = $"{victimName} chưa thực hiện checkin an toàn hôm nay. Vui lòng liên hệ xác nhận.";
                var jsonData = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["type"] = "emergency_alert",
                    ["alertIncidentId"] = incident.Id.ToString(),
                    ["victimUserId"] = record.UserId.ToString(),
                    ["victimName"] = victimName
                });

                var deliveries = new List<NotificationDelivery>();
                foreach (var contact in activeContacts)
                {
                    try
                    {
                        await ProcessContactAsync(
                            contact, incident, record.UserId,
                            title, body, jsonData, deliveries, ct);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex,
                            "Phase 3 failed for ContactId={ContactId} UserId={UserId}",
                            contact.Id, record.UserId);
                    }
                }

                if (deliveries.Count > 0)
                {
                    await notifDeliveryRepo.AddRangeAsync(deliveries, ct);
                    await notifDeliveryRepo.SaveChangesAsync(ct);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SafetyMissedCheckerJob Phase 3 failed entirely — Phase 1 & 2 already committed");
            // Do NOT rethrow — Phase 1 & 2 already committed successfully
        }
    }

    private async Task ProcessContactAsync(
        EmergencyContact contact,
        AlertIncident incident,
        Guid victimUserId,
        string title, string body,
        string jsonData,
        List<NotificationDelivery> deliveries,
        CancellationToken ct)
    {
        // Lookup Beacon account based on channel type; unsupported types (Telegram, etc.) skipped silently
        User? contactUser = contact.ChannelType switch
        {
            ContactChannelType.Email => await userRepo.GetByEmailAsync(contact.ContactValue, ct),
            ContactChannelType.Phone or ContactChannelType.Sms => await userRepo.GetByPhoneAsync(contact.ContactValue, ct),
            _ => null
        };

        if (contact.ChannelType is not (ContactChannelType.Email or ContactChannelType.Phone or ContactChannelType.Sms))
            return;

        if (contactUser is null)
        {
            var failedDelivery = NotificationDelivery.Create(
                userId: victimUserId,
                kind: NotificationKind.EmergencyAlert,
                channel: NotificationChannel.Push,
                recipient: contact.ContactValue,
                title: title,
                body: body,
                alertIncidentId: incident.Id,
                emergencyContactId: contact.Id);
            failedDelivery.MarkFailed("No Beacon account found");
            deliveries.Add(failedDelivery);
            return;
        }

        if (contactUser.Id == victimUserId)
            return; // contact is the victim themselves — skip silently

        var delivery = NotificationDelivery.Create(
            userId: contactUser.Id,
            kind: NotificationKind.EmergencyAlert,
            channel: NotificationChannel.Push,
            recipient: contact.ContactValue,
            title: title,
            body: body,
            alertIncidentId: incident.Id,
            emergencyContactId: contact.Id);

        try
        {
            await notificationService.CreateAndDeliverAsync(
                contactUser.Id, NotificationType.EmergencyAlert, title, body, jsonData, ct);
            delivery.MarkSent();
        }
        catch (Exception ex)
        {
            delivery.MarkFailed("Notification service error");
            logger.LogError(ex, "Failed to create in-app notification for ContactId={ContactId}", contact.Id);
        }

        deliveries.Add(delivery);
    }
}
