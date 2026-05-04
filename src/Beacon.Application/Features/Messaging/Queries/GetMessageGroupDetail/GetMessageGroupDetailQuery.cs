using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Queries.GetMessageGroupDetail;

public record GetMessageGroupDetailQuery(Guid GroupId)
    : IRequest<Result<MessageGroupDetailDto>>;
