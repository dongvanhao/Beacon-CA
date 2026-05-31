using Beacon.Application.Features.Authorization.Dtos;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Permissions.Queries.ListPermissions;

public record ListPermissionsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Group = null) : IRequest<Result<PaginatedList<PermissionDto>>>;
