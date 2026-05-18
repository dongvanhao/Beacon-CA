using Beacon.Application.Features.Posts.Dtos;

namespace Beacon.Application.Features.Messaging.Dtos;

public record MessagePostDto
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
    public string Status { get; init; } = default!;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}
