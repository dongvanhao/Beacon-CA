namespace Beacon.Application.Features.Posts.Dtos;

public record MediaInPostResponse
{
    public Guid Id { get; init; }
    public string Url { get; init; } = default!;
    public string Type { get; init; } = default!;   // "image" | "video"
    public string? ThumbnailUrl { get; init; }
    public int? DurationSeconds { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
}
