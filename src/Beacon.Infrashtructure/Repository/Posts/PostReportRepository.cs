using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository.Posts;
using Beacon.Infrashtructure.Presistence;
using Beacon.Shared.Common.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Posts;

public class PostReportRepository(AppDbContext db) : IPostReportRepository
{
    public Task<PaginatedList<PostReport>> ListAsync(
        string? search,
        PostReportStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = db.PostReports
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.Post)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Reason.ToLower().Contains(keyword) ||
                (x.Description != null && x.Description.ToLower().Contains(keyword)));
        }

        query = query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id);

        return PaginatedList<PostReport>.CreateAsync(query, page, pageSize, ct);
    }

    public Task<PostReport?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.PostReports
            .IgnoreQueryFilters()
            .Include(x => x.Post)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<(int Total, int Pending, int Reviewed, int Resolved, int Rejected)> CountStatusAsync(CancellationToken ct = default)
    {
        var query = db.PostReports.AsNoTracking();
        var total = await query.CountAsync(ct);
        var pending = await query.CountAsync(x => x.Status == PostReportStatus.Pending, ct);
        var reviewed = await query.CountAsync(x => x.Status == PostReportStatus.Reviewed, ct);
        var resolved = await query.CountAsync(x => x.Status == PostReportStatus.Resolved, ct);
        var rejected = await query.CountAsync(x => x.Status == PostReportStatus.Rejected, ct);
        return (total, pending, reviewed, resolved, rejected);
    }

    public async Task AddAsync(PostReport report, CancellationToken ct = default)
        => await db.PostReports.AddAsync(report, ct);

    public void Remove(PostReport report)
        => db.PostReports.Remove(report);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
