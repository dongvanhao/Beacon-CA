namespace Beacon.Application.Features.Posts.Dtos;

public record PostReactionSummaryResponse
{
    public int TotalCount { get; init; }
    public Dictionary<string, int> Icons { get; init; } = new();
}
