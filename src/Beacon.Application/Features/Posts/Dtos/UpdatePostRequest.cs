namespace Beacon.Application.Features.Posts.Dtos;

public record UpdatePostRequest
{
    public string? Caption { get; init; }
    public string? Visibility { get; init; }  // "friends" | "private"
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
}
