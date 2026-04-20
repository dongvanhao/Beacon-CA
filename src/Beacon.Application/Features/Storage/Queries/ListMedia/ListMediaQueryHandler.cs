using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Storage.Dtos;
using Beacon.Application.Mappings.Storage;
using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Storage.Queries.ListMedia;

public class ListMediaQueryHandler(
    IMediaObjectRepository mediaRepository,
    IStorageService storage,
    MediaDtoMapper mapper)
    : IRequestHandler<ListMediaQuery, Result<CursorPagedResult<MediaDto>>>
{
    public async Task<Result<CursorPagedResult<MediaDto>>> Handle(ListMediaQuery query, CancellationToken ct)
    {
        var take = query.Limit + 1;
        var items = await mediaRepository.ListByUserCursorAsync(
            query.CurrentUserId, query.Cursor, take, ct);

        var hasMore = items.Count > query.Limit;
        if (hasMore) items.RemoveAt(items.Count - 1);

        var dtos = await Task.WhenAll(items.Select(async m =>
        {
            var url = await storage.GeneratePresignedGetUrlAsync(m.ObjectKey, ct);
            var thumbUrl = string.IsNullOrWhiteSpace(m.ThumbnailObjectKey)
                ? null
                : await storage.GeneratePresignedGetUrlAsync(m.ThumbnailObjectKey, ct);
            return mapper.ToDto(m, url, thumbUrl);
        }));

        var result = new CursorPagedResult<MediaDto>
        {
            Data = dtos.ToList(),
            Meta = new CursorMeta
            {
                NextCursor = hasMore ? items[^1].CreatedAtUtc : null,
                Limit = query.Limit,
                HasMore = hasMore
            }
        };

        return Result<CursorPagedResult<MediaDto>>.Success(result);
    }
}
