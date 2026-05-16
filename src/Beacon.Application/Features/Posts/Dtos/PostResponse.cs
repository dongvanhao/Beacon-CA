namespace Beacon.Application.Features.Posts.Dtos;

public record PostResponse
{
    public Guid Id { get; init; }
    public Guid OwnerUserId { get; init; }
    public MediaInPostResponse Media { get; init; } = default!;
    public string? Caption { get; init; }
    public string Visibility { get; init; } = default!;
    public string Status { get; init; } = default!;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}
