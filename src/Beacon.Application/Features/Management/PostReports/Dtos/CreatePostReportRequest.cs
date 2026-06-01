namespace Beacon.Application.Features.Management.PostReports.Dtos;

public record CreatePostReportRequest
{
    public Guid PostId { get; init; }
    public Guid ReporterUserId { get; init; }
    public string Reason { get; init; } = default!;
    public string? Description { get; init; }
    public string? Status { get; init; }
}
