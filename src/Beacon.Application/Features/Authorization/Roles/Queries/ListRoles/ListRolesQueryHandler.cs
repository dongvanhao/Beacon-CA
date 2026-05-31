using Beacon.Application.Features.Authorization.Dtos;
using Beacon.Application.Mappings.Authorization;
using Beacon.Domain.IRepository.Identity;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Roles.Queries.ListRoles;

public class ListRolesQueryHandler(
    IRoleRepository roleRepository,
    RoleMapper mapper) : IRequestHandler<ListRolesQuery, Result<PaginatedList<RoleDto>>>
{
    public async Task<Result<PaginatedList<RoleDto>>> Handle(ListRolesQuery query, CancellationToken ct)
    {
        var roles = await roleRepository.ListWithPermissionsAsync(
            query.Search,
            query.Page,
            query.PageSize,
            ct);

        var items = roles.Items.Select(mapper.ToDto).ToList();

        return Result<PaginatedList<RoleDto>>.Success(new PaginatedList<RoleDto>(
            items,
            roles.TotalCount,
            roles.Page,
            roles.PageSize));
    }
}
