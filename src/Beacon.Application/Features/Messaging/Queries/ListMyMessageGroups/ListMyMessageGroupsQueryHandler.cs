using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Application.Mappings.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Queries.ListMyMessageGroups;

public class ListMyMessageGroupsQueryHandler(
    IMessageGroupRepository groupRepo,
    ICurrentUserService currentUser,
    IStorageService storage,
    MessageGroupMapper mapper)
    : IRequestHandler<ListMyMessageGroupsQuery, Result<CursorPagedResult<MessageGroupDto>>>
{
    public async Task<Result<CursorPagedResult<MessageGroupDto>>> Handle(
        ListMyMessageGroupsQuery query, CancellationToken ct)
    {
        var limit = Math.Clamp(query.Limit, 1, 100);
        var paged = await groupRepo.ListByUserAsync(currentUser.UserId, query.Cursor, limit, ct);

        // Batch-resolve presigned URLs for groups with custom avatars
        var objectKeys = paged.Data
            .Where(s => s.AvatarObjectKey is not null)
            .Select(s => s.AvatarObjectKey!)
            .Distinct()
            .ToList();

        var urlMap = new Dictionary<string, string>();
        if (objectKeys.Count > 0)
        {
            var urlResults = await Task.WhenAll(
                objectKeys.Select(async key => (key, url: await storage.GeneratePresignedGetUrlAsync(key, ct))));
            foreach (var (key, url) in urlResults)
                urlMap[key] = url;
        }

        var dtos = paged.Data.Select(s =>
        {
            string? avatarUrl = s.AvatarObjectKey is not null && urlMap.TryGetValue(s.AvatarObjectKey, out var url)
                ? url : null;
            return mapper.ToDto(s, avatarUrl);
        }).ToList();

        return Result<CursorPagedResult<MessageGroupDto>>.Success(new CursorPagedResult<MessageGroupDto>
        {
            Data = dtos,
            Meta = paged.Meta
        });
    }
}
