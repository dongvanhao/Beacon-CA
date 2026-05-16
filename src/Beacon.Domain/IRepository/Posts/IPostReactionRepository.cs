using Beacon.Domain.Entities.Posts;

namespace Beacon.Domain.IRepository.Posts;

public interface IPostReactionRepository
{
    Task<PostReaction?> GetByPostAndUserAsync(
        Guid postId, Guid userId, CancellationToken ct = default);

    Task AddAsync(PostReaction reaction, CancellationToken ct = default);

    void Remove(PostReaction reaction);

    /// <summary>Batch load reactions cho danh sách postIds — tránh N+1.</summary>
    Task<List<PostReaction>> GetByPostIdsAsync(
        IEnumerable<Guid> postIds, CancellationToken ct = default);

    /// <summary>Batch load myReaction của currentUser cho danh sách postIds.</summary>
    Task<List<PostReaction>> GetByPostIdsForUserAsync(
        IEnumerable<Guid> postIds, Guid userId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
