namespace Beacon.Application.Features.Posts.Dtos;

public record PostReactionItemResponse
{
    public Guid ReactionId { get; init; }
    public string Icon { get; init; } = string.Empty;
    public DateTime ReactedAtUtc { get; init; }
    public ReactorUserResponse User { get; init; } = default!;
}
