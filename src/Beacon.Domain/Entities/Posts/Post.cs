using Beacon.Domain.Common;
using Beacon.Domain.Enums;

namespace Beacon.Domain.Entities.Posts;

public class Post : AuditableEntity
{
    public Guid OwnerUserId { get; private set; }
    public Guid MediaId { get; private set; }
    public string? Caption { get; private set; }
    public PostVisibility Visibility { get; private set; }
    public PostStatus Status { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    public bool IsDeleted => DeletedAtUtc.HasValue;

    protected Post() { }

    public static Post Create(
        Guid ownerUserId,
        Guid mediaId,
        string? caption,
        PostVisibility visibility) => new()
    {
        OwnerUserId = ownerUserId,
        MediaId = mediaId,
        Caption = caption?.Trim(),
        Visibility = visibility,
        Status = PostStatus.Active,
        DeletedAtUtc = null
    };

    public void UpdateContent(string? caption, PostVisibility visibility)
    {
        Caption = caption?.Trim();
        Visibility = visibility;
    }

    public void SoftDelete()
        => DeletedAtUtc = DateTime.UtcNow;
}
