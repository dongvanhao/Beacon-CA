using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository.Posts;
using Beacon.Infrashtructure.Presistence;
using Beacon.Shared.Common.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Posts;

public class PostRepository(AppDbContext db) : IPostRepository
{
    public Task<Post?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Posts
            .Include(p => p.DailySafetyRecord)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Post?> GetByIdForManagementAsync(Guid id, CancellationToken ct = default)
        => db.Posts
            .IgnoreQueryFilters()
            .Include(p => p.DailySafetyRecord)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<PaginatedList<Post>> ListForManagementAsync(
        string? search,
        bool? isDeleted,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = db.Posts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AsQueryable();

        if (isDeleted.HasValue)
            query = isDeleted.Value
                ? query.Where(p => p.DeletedAtUtc != null)
                : query.Where(p => p.DeletedAtUtc == null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLowerInvariant();
            query = query.Where(p =>
                p.Caption != null && p.Caption.ToLower().Contains(keyword));
        }

        query = query
            .OrderByDescending(p => p.CreatedAtUtc)
            .ThenByDescending(p => p.Id);

        return PaginatedList<Post>.CreateAsync(query, page, pageSize, ct);
    }

    public Task<int> CountForManagementAsync(CancellationToken ct = default)
        => db.Posts.IgnoreQueryFilters().CountAsync(ct);

    public async Task<(int Total, int Active, int Hidden, int Deleted, int NotDeleted)> CountStatusForManagementAsync(CancellationToken ct = default)
    {
        var query = db.Posts.IgnoreQueryFilters();
        var total = await query.CountAsync(ct);
        var active = await query.CountAsync(p => p.DeletedAtUtc == null && p.Status == PostStatus.Active, ct);
        var hidden = await query.CountAsync(p => p.DeletedAtUtc == null && p.Status == PostStatus.Hidden, ct);
        var deleted = await query.CountAsync(p => p.DeletedAtUtc != null, ct);
        var notDeleted = total - deleted;
        return (total, active, hidden, deleted, notDeleted);
    }

    public async Task<IReadOnlyList<(DateOnly Date, int Count)>> CountCreatedByDateAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default)
    {
        var from = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toExclusive = toDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var rows = await db.Posts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.CreatedAtUtc >= from && p.CreatedAtUtc < toExclusive)
            .GroupBy(p => p.CreatedAtUtc.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var map = rows.ToDictionary(x => DateOnly.FromDateTime(x.Date), x => x.Count);
        var result = new List<(DateOnly Date, int Count)>();

        for (var date = fromDate; date <= toDate; date = date.AddDays(1))
            result.Add((date, map.GetValueOrDefault(date)));

        return result;
    }

    public async Task AddAsync(Post post, CancellationToken ct = default)
        => await db.Posts.AddAsync(post, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);

    public async Task<List<Post>> GetFeedAsync(
        Guid currentUserId,
        List<Guid> friendIds,
        DateTime? cursorCreatedAt,
        Guid? cursorId,
        int limit,
        CancellationToken ct = default)
    {
        var query = db.Posts
            .Include(p => p.DailySafetyRecord)
            .Where(p => p.Status == PostStatus.Active)
            .Where(p =>
                p.OwnerUserId == currentUserId
                || (friendIds.Contains(p.OwnerUserId) && p.Visibility == PostVisibility.Friends))
            .AsQueryable();

        if (cursorCreatedAt.HasValue && cursorId.HasValue)
            query = query.Where(p =>
                p.CreatedAtUtc < cursorCreatedAt.Value
                || (p.CreatedAtUtc == cursorCreatedAt.Value && p.Id.CompareTo(cursorId.Value) < 0));

        return await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .ThenByDescending(p => p.Id)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<List<Post>> GetMyPostsAsync(
        Guid currentUserId,
        DateTime? cursorCreatedAt,
        Guid? cursorId,
        int limit,
        CancellationToken ct = default)
    {
        var query = db.Posts
            .Include(p => p.DailySafetyRecord)
            .Where(p => p.OwnerUserId == currentUserId && p.Status == PostStatus.Active)
            .AsQueryable();

        if (cursorCreatedAt.HasValue && cursorId.HasValue)
            query = query.Where(p =>
                p.CreatedAtUtc < cursorCreatedAt.Value
                || (p.CreatedAtUtc == cursorCreatedAt.Value && p.Id.CompareTo(cursorId.Value) < 0));

        return await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .ThenByDescending(p => p.Id)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<List<Post>> GetFriendsPostsAsync(
        List<Guid> friendIds,
        DateTime? cursorCreatedAt,
        Guid? cursorId,
        int limit,
        CancellationToken ct = default)
    {
        if (friendIds.Count == 0)
            return new List<Post>();

        var query = db.Posts
            .Include(p => p.DailySafetyRecord)
            .Where(p => friendIds.Contains(p.OwnerUserId)
                        && p.Visibility == PostVisibility.Friends
                        && p.Status == PostStatus.Active)
            .AsQueryable();

        if (cursorCreatedAt.HasValue && cursorId.HasValue)
            query = query.Where(p =>
                p.CreatedAtUtc < cursorCreatedAt.Value
                || (p.CreatedAtUtc == cursorCreatedAt.Value && p.Id.CompareTo(cursorId.Value) < 0));

        return await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .ThenByDescending(p => p.Id)
            .Take(limit)
            .ToListAsync(ct);
    }
}
