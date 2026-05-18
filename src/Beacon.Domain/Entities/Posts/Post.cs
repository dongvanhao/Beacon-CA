using Beacon.Domain.Common;
using Beacon.Domain.Entities.Safety;
using Beacon.Domain.Enums;

namespace Beacon.Domain.Entities.Posts;

public class Post : AuditableEntity
{
    public Guid OwnerUserId { get; private set; }
    public Guid MediaId { get; private set; }
    public string? Caption { get; private set; }
    public Guid? DailySafetyRecordId { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public PostVisibility Visibility { get; private set; }
    public PostStatus Status { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    public DailySafetyRecord? DailySafetyRecord { get; private set; }

    public bool IsDeleted => DeletedAtUtc.HasValue;

    protected Post() { }

    public static Post Create(
        Guid ownerUserId,
        Guid mediaId,
        string? caption,
        PostVisibility visibility,
        Guid? dailySafetyRecordId = null,
        decimal? latitude = null,
        decimal? longitude = null) => new()
    {
        OwnerUserId = ownerUserId,
        MediaId = mediaId,
        Caption = caption?.Trim(),
        DailySafetyRecordId = dailySafetyRecordId,
        Latitude = latitude,
        Longitude = longitude,
        Visibility = visibility,
        Status = PostStatus.Active,
        DeletedAtUtc = null
    };

    public void UpdateContent(string? caption, PostVisibility visibility, decimal? latitude, decimal? longitude)
    {
        Caption = caption?.Trim();
        Visibility = visibility;
        Latitude = latitude;
        Longitude = longitude;
    }

    public void SoftDelete()
        => DeletedAtUtc = DateTime.UtcNow;
}
