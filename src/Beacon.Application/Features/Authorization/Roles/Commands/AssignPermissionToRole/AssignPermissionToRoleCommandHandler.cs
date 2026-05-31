using Beacon.Application.Features.Authorization.Dtos;
using Beacon.Application.Mappings.Authorization;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository.Identity;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Roles.Commands.AssignPermissionToRole;

public class AssignPermissionToRoleCommandHandler(
    IRoleRepository roleRepository,
    IPermissionRepository permissionRepository,
    RoleMapper mapper) : IRequestHandler<AssignPermissionToRoleCommand, Result<RoleDto>>
{
    public async Task<Result<RoleDto>> Handle(AssignPermissionToRoleCommand command, CancellationToken ct)
    {
        var role = await roleRepository.GetByIdAsync(command.RoleId, ct);
        if (role is null)
            return Result<RoleDto>.Failure(
                Error.NotFound(ErrorCodes.Authorization.ROLE_NOT_FOUND, "Khong tim thay role."));

        if (!role.IsActive)
            return Result<RoleDto>.Failure(
                Error.Conflict(ErrorCodes.Authorization.ROLE_INACTIVE, "Role da bi vo hieu hoa."));

        var permission = await permissionRepository.GetByIdAsync(command.PermissionId, ct);
        if (permission is null)
            return Result<RoleDto>.Failure(
                Error.NotFound(ErrorCodes.Authorization.PERMISSION_NOT_FOUND, "Khong tim thay permission."));

        var rolePermission = await roleRepository.GetRolePermissionAsync(role.Id, permission.Id, ct);
        if (rolePermission is null)
        {
            await roleRepository.AddRolePermissionAsync(RolePermission.Create(role.Id, permission.Id), ct);
        }
        else
        {
            roleRepository.RemoveRolePermission(rolePermission);
        }

        await roleRepository.SaveChangesAsync(ct);

        var updatedRole = await roleRepository.GetByIdWithPermissionsNoTrackingAsync(role.Id, ct);
        return Result<RoleDto>.Success(mapper.ToDto(updatedRole!));
    }
}
