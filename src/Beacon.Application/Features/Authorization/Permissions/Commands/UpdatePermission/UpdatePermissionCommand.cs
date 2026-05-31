using Beacon.Application.Features.Authorization.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Permissions.Commands.UpdatePermission;

public record UpdatePermissionCommand(Guid Id, UpdatePermissionRequest Request) : IRequest<Result<PermissionDto>>;
