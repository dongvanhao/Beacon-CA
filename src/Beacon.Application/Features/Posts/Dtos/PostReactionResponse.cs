namespace Beacon.Application.Features.Posts.Dtos;

public record PostReactionResponse
{
    public Guid PostId { get; init; }
    public MyReactionResponse? MyReaction { get; init; }
    public PostReactionSummaryResponse ReactionSummary { get; init; } = default!;
}
