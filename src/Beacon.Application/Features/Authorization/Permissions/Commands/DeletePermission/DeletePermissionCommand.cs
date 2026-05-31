using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Permissions.Commands.DeletePermission;

public record DeletePermissionCommand(Guid Id) : IRequest<Result>;
