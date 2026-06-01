using Beacon.Application.Features.AccountManagement.Dtos;
using Beacon.Domain.Entities.Identity;

namespace Beacon.Application.Mappings.AccountManagement;

public sealed class AccountManagementMapper
{
    public AdminAccountDto ToDto(Admin admin)
        => new()
        {
            Id = admin.Id,
            Username = admin.Username,
            FullName = admin.FullName,
            IsActive = admin.IsActive,
            LastLoginAtUtc = admin.LastLoginAtUtc,
            CreatedAtUtc = admin.CreatedAtUtc,
            Roles = admin.AdminRoles
                .Where(ar => ar.Role is not null)
                .Select(ar => new AdminAccountRoleDto
                {
                    Id = ar.Role.Id,
                    Name = ar.Role.Name,
                    Description = ar.Role.Description,
                    IsActive = ar.Role.IsActive
                })
                .OrderBy(r => r.Name)
                .ToList()
        };

    public UserAccountDto ToDto(User user)
        => new()
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FamilyName = user.FamilyName,
            GivenName = user.GivenName,
            PhoneNumber = user.PhoneNumber,
            TimeZone = user.TimeZone,
            IsActive = user.IsActive,
            IsEmailVerified = user.IsEmailVerified,
            AvatarMediaObjectId = user.AvatarMediaObjectId,
            LastLoginAtUtc = user.LastLoginAtUtc,
            LastActiveAtUtc = user.LastActiveAtUtc,
            CreatedAtUtc = user.CreatedAtUtc
        };
}
