using Beacon.Application.Features.Authorization.Dtos;
using Beacon.Application.Mappings.Authorization;
using Beacon.Domain.IRepository.Identity;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Roles.Queries.GetRoleById;

public class GetRoleByIdQueryHandler(
    IRoleRepository roleRepository,
    RoleMapper mapper) : IRequestHandler<GetRoleByIdQuery, Result<RoleDto>>
{
    public async Task<Result<RoleDto>> Handle(GetRoleByIdQuery query, CancellationToken ct)
    {
        var role = await roleRepository.GetByIdWithPermissionsNoTrackingAsync(query.Id, ct);
        if (role is null)
            return Result<RoleDto>.Failure(
                Error.NotFound(ErrorCodes.Authorization.ROLE_NOT_FOUND, "Không tìm thấy role."));

        return Result<RoleDto>.Success(mapper.ToDto(role));
    }
}
