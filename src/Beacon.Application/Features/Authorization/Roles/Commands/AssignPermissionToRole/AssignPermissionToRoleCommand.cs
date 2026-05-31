using Beacon.Application.Features.Authorization.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Roles.Commands.AssignPermissionToRole;

public record AssignPermissionToRoleCommand(Guid RoleId, Guid PermissionId) : IRequest<Result<RoleDto>>;
