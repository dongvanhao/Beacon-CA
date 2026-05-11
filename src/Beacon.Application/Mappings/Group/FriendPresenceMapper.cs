using Beacon.Application.Features.Group.Dtos;
using Beacon.Domain.Entities.Identity;

namespace Beacon.Application.Mappings.Group;

public sealed class FriendPresenceMapper
{
    public FriendPresenceDto ToDto(User user, string? avatarUrl, bool isOnline)
        => new(
            UserId: user.Id,
            FamilyName: user.FamilyName,
            GivenName: user.GivenName,
            AvatarUrl: avatarUrl,
            IsOnline: isOnline,
            LastActiveAtUtc: user.LastActiveAtUtc);
}
