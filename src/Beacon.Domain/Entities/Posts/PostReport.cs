using Beacon.Domain.Common;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Enums;

namespace Beacon.Domain.Entities.Posts;

public class PostReport : AuditableEntity
{
    public Guid PostId { get; private set; }
    public Guid ReporterUserId { get; private set; }
    public string Reason { get; private set; } = default!;
    public string? Description { get; private set; }
    public PostReportStatus Status { get; private set; }
    public Guid? ReviewedByAdminId { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public string? ResolutionNote { get; private set; }

    public Post? Post { get; private set; }
    public User? ReporterUser { get; private set; }
    public Admin? ReviewedByAdmin { get; private set; }

    protected PostReport() { }

    public static PostReport Create(
        Guid postId,
        Guid reporterUserId,
        string reason,
        string? description,
        PostReportStatus status = PostReportStatus.Pending)
        => new()
        {
            PostId = postId,
            ReporterUserId = reporterUserId,
            Reason = reason.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Status = status
        };

    public void Update(string reason, string? description)
    {
        Reason = reason.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public void SetStatus(PostReportStatus status, Guid? reviewedByAdminId, string? resolutionNote)
    {
        Status = status;
        ResolutionNote = string.IsNullOrWhiteSpace(resolutionNote) ? null : resolutionNote.Trim();

        if (status != PostReportStatus.Pending)
        {
            ReviewedByAdminId = reviewedByAdminId;
            ReviewedAtUtc = DateTime.UtcNow;
        }
    }
}
