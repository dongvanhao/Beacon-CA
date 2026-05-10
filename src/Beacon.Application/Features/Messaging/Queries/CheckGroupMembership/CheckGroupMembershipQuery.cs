using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Queries.CheckGroupMembership;

public record CheckGroupMembershipQuery(Guid UserId, Guid GroupId) : IRequest<Result<bool>>;
