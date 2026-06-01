using Beacon.Application.Features.Management.Posts.Dtos;

namespace Beacon.Application.Features.Management.PostReports.Dtos;

public record PostReportDto
{
    public Guid Id { get; init; }
    public Guid PostId { get; init; }
    public PostManagementDto? Post { get; init; }
    public Guid ReporterUserId { get; init; }
    public string Reason { get; init; } = default!;
    public string? Description { get; init; }
    public string Status { get; init; } = default!;
    public Guid? ReviewedByAdminId { get; init; }
    public DateTime? ReviewedAtUtc { get; init; }
    public string? ResolutionNote { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}
