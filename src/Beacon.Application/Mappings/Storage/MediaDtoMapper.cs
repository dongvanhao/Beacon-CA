using Beacon.Application.Features.Storage.Dtos;
using Beacon.Domain.Entities.Storage;
using Beacon.Domain.Enums;

namespace Beacon.Application.Mappings.Storage;

public sealed class MediaDtoMapper
{
    public MediaDto ToDto(MediaObject media, string url, string? thumbnailUrl)
        => new()
        {
            Id = media.Id,
            Url = url,
            ThumbnailUrl = thumbnailUrl,
            ObjectKey = media.ObjectKey,
            Type = media.MediaType == MediaType.Image ? "image" : "video",
            MimeType = media.ContentType,
            Size = media.FileSizeBytes,
            Width = media.Width,
            Height = media.Height,
            CreatedAt = media.CreatedAtUtc,
            CreatedBy = media.UploadProviderByUserId
        };
}
