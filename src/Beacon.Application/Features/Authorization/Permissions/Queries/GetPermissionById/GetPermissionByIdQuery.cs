using Beacon.Application.Features.Authorization.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Permissions.Queries.GetPermissionById;

public record GetPermissionByIdQuery(Guid Id) : IRequest<Result<PermissionDto>>;
