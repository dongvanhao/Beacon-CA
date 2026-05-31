using Beacon.Domain.IRepository.Identity;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Permissions.Queries.ListPermissionGroups;

public class ListPermissionGroupsQueryHandler(IPermissionRepository permissionRepository)
    : IRequestHandler<ListPermissionGroupsQuery, Result<IReadOnlyList<string>>>
{
    public async Task<Result<IReadOnlyList<string>>> Handle(
        ListPermissionGroupsQuery query,
        CancellationToken ct)
    {
        var groups = await permissionRepository.ListGroupsAsync(ct);
        return Result<IReadOnlyList<string>>.Success(groups);
    }
}
