using Beacon.Application.Features.Group.Dtos;
using Beacon.Domain.Entities.Group;

namespace Beacon.Application.Mappings.Group;

public sealed class FriendMapper
{
    public FriendDto ToDto(Friend f, Guid currentUserId, string familyName, string givenName, string? avatarUrl)
        => new(
            UserId: currentUserId == f.UserId1 ? f.UserId2 : f.UserId1,
            FamilyName: familyName,
            GivenName: givenName,
            AvatarUrl: avatarUrl,
            Type: f.Type,
            CreatedAtUtc: f.CreatedAtUtc,
            MessageGroupId: f.MessageGroupId);
}
