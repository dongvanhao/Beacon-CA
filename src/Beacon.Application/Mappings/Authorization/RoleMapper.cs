using Beacon.Application.Features.Authorization.Dtos;
using Beacon.Domain.Entities.Identity;

namespace Beacon.Application.Mappings.Authorization;

public sealed class RoleMapper(PermissionMapper permissionMapper)
{
    public RoleDto ToDto(Role role)
        => new()
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            IsActive = role.IsActive,
            CreatedAtUtc = role.CreatedAtUtc,
            Permissions = role.RolePermissions
                .Where(rp => rp.Permission is not null)
                .Select(rp => permissionMapper.ToDto(rp.Permission))
                .OrderBy(p => p.Group)
                .ThenBy(p => p.Name)
                .ToList()
        };

    public AdminRoleAssignmentDto ToAdminRoleAssignmentDto(Admin admin, Role role, AdminRole adminRole)
        => new()
        {
            AdminId = admin.Id,
            Username = admin.Username,
            RoleId = role.Id,
            RoleName = role.Name,
            AssignedAtUtc = adminRole.AssignedAtUtc
        };
}
