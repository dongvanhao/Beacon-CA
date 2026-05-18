namespace Beacon.Application.Features.Posts.Dtos;

public record ReactorUserResponse
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
}
