namespace Beacon.Application.Features.Posts.Dtos;

public record OwnerInPostResponse
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = default!;
    public string? AvatarUrl { get; init; }
}
