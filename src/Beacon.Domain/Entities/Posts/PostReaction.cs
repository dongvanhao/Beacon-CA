using Beacon.Domain.Common;

namespace Beacon.Domain.Entities.Posts;

public class PostReaction : AuditableEntity
{
    public Guid PostId { get; private set; }
    public Guid UserId { get; private set; }
    public string Icon { get; private set; } = default!;

    protected PostReaction() { }

    public static PostReaction Create(Guid postId, Guid userId, string icon) => new()
    {
        PostId = postId,
        UserId = userId,
        Icon = icon
    };

    public void UpdateIcon(string newIcon) => Icon = newIcon;
}
