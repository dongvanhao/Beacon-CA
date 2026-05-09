using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.TransferOwnership;

public record TransferOwnershipCommand(Guid GroupId, Guid NewOwnerUserId) : IRequest<Result>;
