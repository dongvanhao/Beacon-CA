using Beacon.Application.Features.Identity.Dtos;
using Beacon.Domain.Entities.Identity;

namespace Beacon.Application.Mappings.Identity;

public sealed class UserProfileMapper
{
    public UserProfileDto ToProfileDto(User user, string? avatarUrl = null)
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
            LastLoginAtUtc = user.LastLoginAtUtc,
            CreatedAtUtc = user.CreatedAtUtc,
            AvatarMediaObjectId = user.AvatarMediaObjectId,
            AvatarUrl = avatarUrl,
        };
}
