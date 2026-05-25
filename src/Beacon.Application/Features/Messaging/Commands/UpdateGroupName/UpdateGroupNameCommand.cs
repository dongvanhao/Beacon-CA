using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.UpdateGroupName;

public record UpdateGroupNameCommand(Guid GroupId, string Name) : IRequest<Result>;
