using Beacon.Domain.Entities.Storage;

namespace Beacon.Domain.IRepository.Storage;

public interface IMediaObjectRepository
{
    Task<MediaObject?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<MediaObject?> GetByIdIncludeDeletedAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(MediaObject media, CancellationToken ct = default);

    Task<List<MediaObject>> ListByUserCursorAsync(
        Guid userId,
        DateTime? cursor,
        int take,
        CancellationToken ct = default);

    void Remove(MediaObject media);

    Task SaveChangesAsync(CancellationToken ct = default);
}
