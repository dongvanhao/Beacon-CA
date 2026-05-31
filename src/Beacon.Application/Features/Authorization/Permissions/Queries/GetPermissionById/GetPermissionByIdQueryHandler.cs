using Beacon.Application.Features.Authorization.Dtos;
using Beacon.Application.Mappings.Authorization;
using Beacon.Domain.IRepository.Identity;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Permissions.Queries.GetPermissionById;

public class GetPermissionByIdQueryHandler(
    IPermissionRepository permissionRepository,
    PermissionMapper mapper) : IRequestHandler<GetPermissionByIdQuery, Result<PermissionDto>>
{
    public async Task<Result<PermissionDto>> Handle(GetPermissionByIdQuery query, CancellationToken ct)
    {
        var permission = await permissionRepository.GetByIdAsync(query.Id, ct);
        if (permission is null)
            return Result<PermissionDto>.Failure(
                Error.NotFound(ErrorCodes.Authorization.PERMISSION_NOT_FOUND, "Không tìm thấy permission."));

        return Result<PermissionDto>.Success(mapper.ToDto(permission));
    }
}
