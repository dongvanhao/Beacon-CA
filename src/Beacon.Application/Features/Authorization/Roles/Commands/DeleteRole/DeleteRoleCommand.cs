using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Roles.Commands.DeleteRole;

public record DeleteRoleCommand(Guid Id) : IRequest<Result>;
