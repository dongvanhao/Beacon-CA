using Beacon.Application.Features.Posts.Dtos;
using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Entities.Storage;

namespace Beacon.Application.Mappings.Posts;

public sealed class PostDtoMapper
{
    public PostResponse ToPostResponse(Post post, MediaInPostResponse media) => new()
    {
        Id = post.Id,
        OwnerUserId = post.OwnerUserId,
        Media = media,
        Caption = post.Caption,
        Visibility = post.Visibility.ToString().ToLowerInvariant(),
        Status = post.Status.ToString().ToLowerInvariant(),
        CreatedAtUtc = post.CreatedAtUtc,
        UpdatedAtUtc = post.UpdatedAtUtc
    };

    public MediaInPostResponse ToMediaResponse(MediaObject media, string url, string? thumbnailUrl) => new()
    {
        Id = media.Id,
        Url = url,
        Type = media.MediaType.ToString().ToLowerInvariant(),
        ThumbnailUrl = thumbnailUrl,
        DurationSeconds = media.DurationSeconds,
        Width = media.Width,
        Height = media.Height
    };
}
