using Beacon.Application.Features.Authorization.Dtos;
using Beacon.Domain.Entities.Identity;

namespace Beacon.Application.Mappings.Authorization;

public sealed class PermissionMapper
{
    public PermissionDto ToDto(Permission permission)
        => new()
        {
            Id = permission.Id,
            Name = permission.Name,
            Description = permission.Description,
            Group = permission.Group
        };
}
