namespace Beacon.Application.Features.Management.PostReports.Dtos;

public record UpdatePostReportRequest
{
    public string? Reason { get; init; }
    public string? Description { get; init; }
    public string? Status { get; init; }
    public string? ResolutionNote { get; init; }
}
