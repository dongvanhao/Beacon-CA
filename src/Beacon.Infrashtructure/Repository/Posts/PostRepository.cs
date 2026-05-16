using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository.Posts;
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Posts;

public class PostRepository(AppDbContext db) : IPostRepository
{
    public Task<Post?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Posts.FirstOrDefaultAsync(p => p.Id == id, ct);

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
