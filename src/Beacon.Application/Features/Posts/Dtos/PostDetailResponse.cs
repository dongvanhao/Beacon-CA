namespace Beacon.Application.Features.Posts.Dtos;

public record PostDetailResponse : PostResponse
{
    public OwnerInPostResponse Owner { get; init; } = default!;
    public PostReactionSummaryResponse ReactionSummary { get; init; } = default!;
    public MyReactionResponse? MyReaction { get; init; }
}
