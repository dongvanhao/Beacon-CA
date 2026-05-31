using Beacon.Application.Features.Authorization.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Roles.Commands.UpdateRole;

public record UpdateRoleCommand(Guid Id, UpdateRoleRequest Request) : IRequest<Result<RoleDto>>;
