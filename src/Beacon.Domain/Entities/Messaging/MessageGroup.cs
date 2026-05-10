using Beacon.Domain.Common;
using Beacon.Domain.Entities.Storage;
using Beacon.Domain.Enums.Messaging;

namespace Beacon.Domain.Entities.Messaging;

public class MessageGroup : BaseEntity
{
    public MessageGroupType Type { get; set; }
    public string? DirectKey { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? Name { get; set; }
    public Guid? AvatarMediaObjectId { get; set; }
    public MediaObject? AvatarMedia { get; set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public ICollection<MessageGroupMember> Members { get; set; } = [];
    public ICollection<Message> Messages { get; set; } = [];

    public static string BuildDirectKey(Guid userA, Guid userB)
    {
        var ids = new[] { userA, userB }.OrderBy(id => id).ToArray();
        return $"{ids[0]}_{ids[1]}";
    }

    public void Delete()
    {
        IsDeleted = true;
        DeletedAtUtc = DateTime.UtcNow;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAtUtc = null;
    }
}
