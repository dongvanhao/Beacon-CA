namespace Beacon.Application.Features.Posts.Dtos;

public record UpsertPostReactionRequest
{
    public string Icon { get; init; } = default!;
}
