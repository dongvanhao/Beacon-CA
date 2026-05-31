using Beacon.Application.Features.Authorization.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Permissions.Commands.UpsertPermissionCatalog;

public record UpsertPermissionCatalogCommand : IRequest<Result<UpsertPermissionCatalogResultDto>>;
