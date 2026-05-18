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

    /// <summary>Keyset cursor pagination theo CreatedAtUtc DESC, optional filter by icon. Trả (items, hasMore).</summary>
    Task<(List<PostReaction> Items, bool HasMore)> GetPagedByPostIdAsync(
        Guid postId,
        string? iconFilter,
        DateTime? cursor,
        int limit,
        CancellationToken ct = default);

    /// <summary>Load tất cả reactions của bài — dùng tính summary tổng (không bị ảnh hưởng bởi iconFilter).</summary>
    Task<List<PostReaction>> GetAllByPostIdAsync(
        Guid postId,
        CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
