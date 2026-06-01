using Beacon.Domain.Entities.Storage;
using Beacon.Domain.IRepository.Storage;
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Storage;

public class MediaObjectRepository(AppDbContext context) : IMediaObjectRepository
{
    public async Task<MediaObject?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.MediaObjects.FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<MediaObject?> GetByIdIncludeDeletedAsync(Guid id, CancellationToken ct = default)
        => await context.MediaObjects
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<List<MediaObject>> ListByIdsIncludeDeletedAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => await context.MediaObjects
            .IgnoreQueryFilters()
            .Where(m => ids.Contains(m.Id))
            .ToListAsync(ct);

    public async Task AddAsync(MediaObject media, CancellationToken ct = default)
        => await context.MediaObjects.AddAsync(media, ct);

    public async Task<List<MediaObject>> ListByUserCursorAsync(
        Guid userId,
        DateTime? cursor,
        int take,
        CancellationToken ct = default)
    {
        var query = context.MediaObjects
            .Where(m => m.UploadProviderByUserId == userId);

        if (cursor.HasValue)
            query = query.Where(m => m.CreatedAtUtc < cursor.Value);

        return await query
            .OrderByDescending(m => m.CreatedAtUtc)
            .ThenByDescending(m => m.Id)
            .Take(take)
            .ToListAsync(ct);
    }

    public void Remove(MediaObject media)
        => context.MediaObjects.Remove(media);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
