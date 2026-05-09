using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.CreateGroup;

public record CreateGroupCommand(string Name, Guid? AvatarMediaObjectId) : IRequest<Result<MessageGroupDetailDto>>;
