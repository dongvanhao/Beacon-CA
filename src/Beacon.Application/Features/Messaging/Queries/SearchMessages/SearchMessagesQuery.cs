using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Queries.SearchMessages;

public record SearchMessagesQuery(Guid GroupId, string Search, long? Cursor, int Limit = 20)
    : IRequest<Result<CursorPagedResult<MessageDto, long>>>;
