using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Enums;
using Beacon.Shared.Common.Pagination;

namespace Beacon.Domain.IRepository.Posts;

public interface IPostReportRepository
{
    Task<PaginatedList<PostReport>> ListAsync(
        string? search,
        PostReportStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<PostReport?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(int Total, int Pending, int Reviewed, int Resolved, int Rejected)> CountStatusAsync(CancellationToken ct = default);
    Task AddAsync(PostReport report, CancellationToken ct = default);
    void Remove(PostReport report);
    Task SaveChangesAsync(CancellationToken ct = default);
}
