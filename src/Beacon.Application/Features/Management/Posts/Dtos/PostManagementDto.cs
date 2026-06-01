namespace Beacon.Application.Features.Management.Posts.Dtos;

public record PostManagementDto
{
    public Guid Id { get; init; }
    public Guid OwnerUserId { get; init; }
    public Guid MediaId { get; init; }
    public ManagedPostMediaDto? Media { get; init; }
    public string? Caption { get; init; }
    public string Visibility { get; init; } = default!;
    public string Status { get; init; } = default!;
    public Guid? DailySafetyRecordId { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public DateTime? DeletedAtUtc { get; init; }
    public string? DeletedReason { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}

public record ManagedPostMediaDto
{
    public Guid Id { get; init; }
    public string Url { get; init; } = default!;
    public string Type { get; init; } = default!;
    public string? ThumbnailUrl { get; init; }
    public int? DurationSeconds { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
}
