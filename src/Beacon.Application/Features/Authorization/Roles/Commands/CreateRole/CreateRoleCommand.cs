using Beacon.Application.Features.Authorization.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Roles.Commands.CreateRole;

public record CreateRoleCommand(CreateRoleRequest Request) : IRequest<Result<RoleDto>>;
