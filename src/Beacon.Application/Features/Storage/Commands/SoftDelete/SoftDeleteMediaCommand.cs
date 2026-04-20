using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Storage.Commands.SoftDelete;

public record SoftDeleteMediaCommand(Guid Id, Guid CurrentUserId) : IRequest<Result>;
