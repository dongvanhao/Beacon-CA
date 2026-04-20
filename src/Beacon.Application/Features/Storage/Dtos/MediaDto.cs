namespace Beacon.Application.Features.Storage.Dtos;

public class MediaDto
{
    public Guid Id { get; set; }
    public string Url { get; set; } = default!;
    public string? ThumbnailUrl { get; set; }
    public string ObjectKey { get; set; } = default!;
    public string Type { get; set; } = default!;
    public string MimeType { get; set; } = default!;
    public long Size { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
}
