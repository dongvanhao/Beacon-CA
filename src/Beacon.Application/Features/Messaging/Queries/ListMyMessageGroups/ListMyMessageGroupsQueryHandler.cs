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
    MessageGroupMapper mapper)
    : IRequestHandler<ListMyMessageGroupsQuery, Result<CursorPagedResult<MessageGroupDto>>>
{
    public async Task<Result<CursorPagedResult<MessageGroupDto>>> Handle(
        ListMyMessageGroupsQuery query, CancellationToken ct)
    {
        var limit = Math.Clamp(query.Limit, 1, 100);
        var paged = await groupRepo.ListByUserAsync(currentUser.UserId, query.Cursor, limit, ct);

        var dtos = paged.Data.Select(mapper.ToDto).ToList();

        return Result<CursorPagedResult<MessageGroupDto>>.Success(new CursorPagedResult<MessageGroupDto>
        {
            Data = dtos,
            Meta = paged.Meta
        });
    }
}
