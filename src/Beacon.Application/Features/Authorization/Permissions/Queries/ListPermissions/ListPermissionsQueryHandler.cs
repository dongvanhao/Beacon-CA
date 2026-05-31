using Beacon.Application.Features.Authorization.Dtos;
using Beacon.Application.Mappings.Authorization;
using Beacon.Domain.IRepository.Identity;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Permissions.Queries.ListPermissions;

public class ListPermissionsQueryHandler(
    IPermissionRepository permissionRepository,
    PermissionMapper mapper) : IRequestHandler<ListPermissionsQuery, Result<PaginatedList<PermissionDto>>>
{
    public async Task<Result<PaginatedList<PermissionDto>>> Handle(ListPermissionsQuery query, CancellationToken ct)
    {
        var permissions = await permissionRepository.ListAsync(
            query.Search,
            query.Group,
            query.Page,
            query.PageSize,
            ct);

        var items = permissions.Items.Select(mapper.ToDto).ToList();

        return Result<PaginatedList<PermissionDto>>.Success(new PaginatedList<PermissionDto>(
            items,
            permissions.TotalCount,
            permissions.Page,
            permissions.PageSize));
    }
}
