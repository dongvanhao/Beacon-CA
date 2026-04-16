using Beacon.Application.Features.Identity.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Identity.Queries;

public record GetCurrentUserQuery(Guid UserId) : IRequest<Result<UserProfileDto>>;
