using Beacon.Domain.Entities.Posts;
using Beacon.Shared.Common.Pagination;

namespace Beacon.Domain.IRepository.Posts;

public interface IPostRepository
{
    Task<Post?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Post?> GetByIdForManagementAsync(Guid id, CancellationToken ct = default);
    Task<PaginatedList<Post>> ListForManagementAsync(
        string? search,
        bool? isDeleted,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<int> CountForManagementAsync(CancellationToken ct = default);
    Task<(int Total, int Active, int Hidden, int Deleted, int NotDeleted)> CountStatusForManagementAsync(CancellationToken ct = default);
    Task<IReadOnlyList<(DateOnly Date, int Count)>> CountCreatedByDateAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);

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
