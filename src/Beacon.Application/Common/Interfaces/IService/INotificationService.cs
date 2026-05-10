using Beacon.Domain.Enums.Group;

namespace Beacon.Application.Common.Interfaces.IService;

public interface INotificationService
{
    Task CreateAndDeliverAsync(
        Guid receiverUserId,
        NotificationType type,
        string title,
        string body,
        string? data = null,
        CancellationToken ct = default);
}
