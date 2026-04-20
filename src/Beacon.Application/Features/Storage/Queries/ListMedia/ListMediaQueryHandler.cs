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

        var urlResults = await storage.GetMediaUrlsBatchAsync(items, ct);
        var dtos = urlResults.Select(r => mapper.ToDto(r.Media, r.Url, r.ThumbUrl)).ToList();

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
