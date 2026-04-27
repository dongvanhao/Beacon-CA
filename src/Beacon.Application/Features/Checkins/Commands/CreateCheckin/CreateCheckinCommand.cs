using Beacon.Application.Features.Checkins.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Checkins.Commands.CreateCheckin;

public record CreateCheckinCommand(Guid UserId, CreateCheckinRequest Request)
    : IRequest<Result<CheckinDto>>;
