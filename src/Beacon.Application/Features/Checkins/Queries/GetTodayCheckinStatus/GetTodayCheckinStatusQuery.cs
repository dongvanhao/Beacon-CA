using Beacon.Application.Features.Checkins.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Checkins.Queries.GetTodayCheckinStatus;

public record GetTodayCheckinStatusQuery(Guid UserId)
    : IRequest<Result<TodayCheckinStatusDto>>;
