using Beacon.Domain.Enums.Group;

namespace Beacon.Application.Features.Group.Dtos;

public record UserSearchDto(
    Guid UserId,
    string FamilyName,
    string GivenName,
    string? AvatarUrl,
    FriendshipStatus FriendshipStatus);
