namespace Beacon.Application.Features.Group.Dtos;

public record FriendRequestDto(
    Guid Id,
    Guid SenderId,
    string SenderUsername,
    string? SenderAvatarUrl,
    DateTime CreatedAtUtc);
