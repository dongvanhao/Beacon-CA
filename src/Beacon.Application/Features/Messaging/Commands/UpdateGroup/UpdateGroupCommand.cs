using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.UpdateGroup;

public record UpdateGroupCommand(Guid GroupId, string? Name, Guid? AvatarMediaObjectId) : IRequest<Result>;
