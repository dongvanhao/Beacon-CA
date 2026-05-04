using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Application.Mappings.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Queries.ListMessages;

public class ListMessagesQueryHandler(
    IMessageGroupRepository groupRepo,
    IMessageRepository messageRepo,
    ICurrentUserService currentUser,
    MessageMapper mapper)
    : IRequestHandler<ListMessagesQuery, Result<CursorPagedResult<MessageDto>>>
{
    public async Task<Result<CursorPagedResult<MessageDto>>> Handle(
        ListMessagesQuery query, CancellationToken ct)
    {
        if (!await groupRepo.IsMemberAsync(query.GroupId, currentUser.UserId, ct))
            return Result<CursorPagedResult<MessageDto>>.Failure(
                Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "Bạn không phải thành viên của nhóm này."));

        var limit = Math.Clamp(query.Limit, 1, 100);
        var paged = await messageRepo.ListByGroupAsync(query.GroupId, query.Cursor, limit, ct);

        var dtos = paged.Data.Select(m => mapper.ToDto(m, m.Sender.Username)).ToList();

        return Result<CursorPagedResult<MessageDto>>.Success(new CursorPagedResult<MessageDto>
        {
            Data = dtos,
            Meta = paged.Meta
        });
    }
}
