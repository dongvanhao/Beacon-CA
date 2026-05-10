using Beacon.Domain.Common;
using Beacon.Domain.Enums.Group;

namespace Beacon.Domain.Entities.Group;

public class Notification : AuditableEntity
{
    public Guid ReceiverUserId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = default!;
    public string Body { get; private set; } = default!;
    public string? Data { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }

    protected Notification() { }

    public static Notification Create(
        Guid receiverUserId,
        NotificationType type,
        string title,
        string body,
        string? data = null)
        => new()
        {
            ReceiverUserId = receiverUserId,
            Type = type,
            Title = title,
            Body = body,
            Data = data,
            IsRead = false
        };

    public void MarkRead()
    {
        if (IsRead) return;
        IsRead = true;
        ReadAtUtc = DateTime.UtcNow;
    }
}
