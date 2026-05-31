using Beacon.Application.Features.Authorization.Dtos;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Roles.Queries.ListRoles;

public record ListRolesQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null) : IRequest<Result<PaginatedList<RoleDto>>>;
