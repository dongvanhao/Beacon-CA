using Beacon.Application.Features.Authorization.Dtos;
using Beacon.Application.Mappings.Authorization;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository.Identity;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Authorization.Roles.Commands.CreateRole;

public class CreateRoleCommandHandler(
    IRoleRepository roleRepository,
    IPermissionRepository permissionRepository,
    RoleMapper mapper) : IRequestHandler<CreateRoleCommand, Result<RoleDto>>
{
    public async Task<Result<RoleDto>> Handle(CreateRoleCommand command, CancellationToken ct)
    {
        var name = command.Request.Name.Trim();
        if (await roleRepository.ExistsByNameAsync(name, ct: ct))
            return Result<RoleDto>.Failure(
                Error.Conflict(ErrorCodes.Authorization.ROLE_ALREADY_EXISTS, "Role da ton tai."));

        var permissionIds = (command.Request.PermissionIds ?? [])
            .Distinct()
            .ToArray();

        if (permissionIds.Length > 0)
        {
            var permissions = await permissionRepository.GetByIdsAsync(permissionIds, ct);
            if (permissions.Count != permissionIds.Length)
                return Result<RoleDto>.Failure(
                    Error.NotFound(ErrorCodes.Authorization.PERMISSION_NOT_FOUND, "Khong tim thay permission."));
        }

        var role = Role.Create(name, Normalize(command.Request.Description));

        await roleRepository.AddAsync(role, ct);
        foreach (var permissionId in permissionIds)
        {
            await roleRepository.AddRolePermissionAsync(RolePermission.Create(role.Id, permissionId), ct);
        }

        await roleRepository.SaveChangesAsync(ct);

        var createdRole = await roleRepository.GetByIdWithPermissionsNoTrackingAsync(role.Id, ct);
        return Result<RoleDto>.Success(mapper.ToDto(createdRole!));
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
