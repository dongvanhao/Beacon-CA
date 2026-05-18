namespace Beacon.Application.Features.Posts.Dtos;

public record FeedPostResponse
{
    public Guid Id { get; init; }
    public Guid OwnerUserId { get; init; }
    public Guid? DailySafetyRecordId { get; init; }
    public DailySafetyRecordInPostResponse? DailySafetyRecord { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public OwnerInPostResponse Owner { get; init; } = default!;
    public MediaInPostResponse Media { get; init; } = default!;
    public string? Caption { get; init; }
    public string Visibility { get; init; } = default!;
    public DateTime CreatedAtUtc { get; init; }
    public PostReactionSummaryResponse ReactionSummary { get; init; } = default!;
    public MyReactionResponse? MyReaction { get; init; }
}

public record FeedResponse
{
    public List<FeedPostResponse> Items { get; init; } = new();
    public string? NextCursor { get; init; }
}
