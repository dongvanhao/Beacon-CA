using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Storage.Commands.HardDelete;

public record HardDeleteMediaCommand(Guid Id) : IRequest<Result>;
