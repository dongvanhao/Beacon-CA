using Beacon.Application.Features.Authorization.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Permissions.Commands.CreatePermission;

public record CreatePermissionCommand(CreatePermissionRequest Request) : IRequest<Result<PermissionDto>>;
