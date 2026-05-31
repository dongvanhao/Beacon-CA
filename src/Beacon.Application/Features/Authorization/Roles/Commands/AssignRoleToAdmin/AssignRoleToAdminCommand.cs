using Beacon.Application.Features.Authorization.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Roles.Commands.AssignRoleToAdmin;

public record AssignRoleToAdminCommand(Guid RoleId, Guid AdminId) : IRequest<Result<AdminRoleAssignmentDto>>;
