using Beacon.Domain.Entities.Posts;

namespace Beacon.Domain.IRepository.Posts;

public interface IPostRepository
{
    Task<Post?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(Post post, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Feed query — trả về posts của currentUser + bạn bè,
    /// với visibility check và cursor pagination.
    /// </summary>
    Task<List<Post>> GetFeedAsync(
        Guid currentUserId,
        List<Guid> friendIds,
        DateTime? cursorCreatedAt,
        Guid? cursorId,
        int limit,
        CancellationToken ct = default);

    Task<List<Post>> GetMyPostsAsync(
        Guid currentUserId,
        DateTime? cursorCreatedAt,
        Guid? cursorId,
        int limit,
        CancellationToken ct = default);

    Task<List<Post>> GetFriendsPostsAsync(
        List<Guid> friendIds,
        DateTime? cursorCreatedAt,
        Guid? cursorId,
        int limit,
        CancellationToken ct = default);
}
