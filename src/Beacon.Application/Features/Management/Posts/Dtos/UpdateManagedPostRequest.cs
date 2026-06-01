namespace Beacon.Application.Features.Management.Posts.Dtos;

public record UpdateManagedPostRequest
{
    public string? Caption { get; init; }
    public string? Visibility { get; init; }
    public string? Status { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
}
