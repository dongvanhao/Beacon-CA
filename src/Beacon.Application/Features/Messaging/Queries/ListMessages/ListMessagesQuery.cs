using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Queries.ListMessages;

public record ListMessagesQuery(Guid GroupId, long? Cursor, int Limit = 20)
    : IRequest<Result<CursorPagedResult<MessageDto, long>>>;
