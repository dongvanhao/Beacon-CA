using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository.Posts;
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Posts;

public class PostReactionRepository(AppDbContext db) : IPostReactionRepository
{
    public Task<PostReaction?> GetByPostAndUserAsync(
        Guid postId, Guid userId, CancellationToken ct = default)
        => db.PostReactions.FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == userId, ct);

    public async Task AddAsync(PostReaction reaction, CancellationToken ct = default)
        => await db.PostReactions.AddAsync(reaction, ct);

    public void Remove(PostReaction reaction)
        => db.PostReactions.Remove(reaction);

    public Task<List<PostReaction>> GetByPostIdsAsync(
        IEnumerable<Guid> postIds, CancellationToken ct = default)
        => db.PostReactions
            .Where(r => postIds.Contains(r.PostId))
            .ToListAsync(ct);

    public Task<List<PostReaction>> GetByPostIdsForUserAsync(
        IEnumerable<Guid> postIds, Guid userId, CancellationToken ct = default)
        => db.PostReactions
            .Where(r => postIds.Contains(r.PostId) && r.UserId == userId)
            .ToListAsync(ct);

    public async Task<(List<PostReaction> Items, bool HasMore)> GetPagedByPostIdAsync(
        Guid postId, string? iconFilter, DateTime? cursor, int limit, CancellationToken ct = default)
    {
        var query = db.PostReactions.Where(r => r.PostId == postId);

        if (!string.IsNullOrEmpty(iconFilter))
        {
            var boundedIcon = $"{ReactionIcons.Separator}{iconFilter}{ReactionIcons.Separator}";
            query = query.Where(r =>
                (ReactionIcons.Separator + r.Icon + ReactionIcons.Separator).Contains(boundedIcon));
        }

        if (cursor.HasValue)
            query = query.Where(r => r.CreatedAtUtc < cursor.Value);

        var items = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(limit + 1)
            .ToListAsync(ct);

        var hasMore = items.Count > limit;
        if (hasMore) items = items.Take(limit).ToList();

        return (items, hasMore);
    }

    public Task<List<PostReaction>> GetAllByPostIdAsync(
        Guid postId, CancellationToken ct = default)
        => db.PostReactions
            .Where(r => r.PostId == postId)
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
