using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.DeleteGroup;

public record DeleteGroupCommand(Guid GroupId) : IRequest<Result>;
