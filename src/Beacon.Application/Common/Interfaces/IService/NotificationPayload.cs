namespace Beacon.Application.Common.Interfaces.IService;

public record NotificationPayload(
    Guid NotificationId,
    string Type,
    string Title,
    string Body,
    string? Data);
