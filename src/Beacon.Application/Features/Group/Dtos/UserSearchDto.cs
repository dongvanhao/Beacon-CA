using Beacon.Domain.Enums.Group;

namespace Beacon.Application.Features.Group.Dtos;

public record UserSearchDto(
    Guid UserId,
    string Username,
    string? AvatarUrl,
    FriendshipStatus FriendshipStatus);
