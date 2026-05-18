namespace Beacon.Application.Features.Posts.Dtos;

public record PostReactionListResponse
{
    public List<PostReactionItemResponse> Items { get; init; } = new();
    public PostReactionSummaryResponse Summary { get; init; } = default!;
    public string? NextCursor { get; init; }
    public bool HasMore { get; init; }
}
