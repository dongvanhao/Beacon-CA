using Beacon.Application.Features.Group.Dtos;
using Beacon.Domain.Entities.Group;

namespace Beacon.Application.Mappings.Group;

public sealed class FriendRequestMapper
{
    public FriendRequestDto ToDto(FriendRequest r, string senderFamilyName, string senderGivenName, string? senderAvatarUrl)
        => new(r.Id, r.SenderId, senderFamilyName, senderGivenName, senderAvatarUrl, r.CreatedAtUtc);
}
