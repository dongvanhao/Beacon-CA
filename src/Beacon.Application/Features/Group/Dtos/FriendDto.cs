using Beacon.Domain.Enums.Group;

namespace Beacon.Application.Features.Group.Dtos;

public record FriendDto(
    Guid UserId,
    string Username,
    string? AvatarUrl,
    FriendType Type,
    DateTime CreatedAtUtc,
    Guid MessageGroupId);
