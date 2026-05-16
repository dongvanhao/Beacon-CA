using Beacon.Domain.Entities.Posts;
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

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
