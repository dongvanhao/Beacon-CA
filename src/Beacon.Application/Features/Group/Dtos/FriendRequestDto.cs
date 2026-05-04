namespace Beacon.Application.Features.Group.Dtos;

public record FriendRequestDto(
    Guid Id,
    Guid SenderId,
    string SenderFamilyName,
    string SenderGivenName,
    string? SenderAvatarUrl,
    DateTime CreatedAtUtc);
