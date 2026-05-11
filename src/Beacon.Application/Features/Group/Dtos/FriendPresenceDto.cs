namespace Beacon.Application.Features.Group.Dtos;

public record FriendPresenceDto(
    Guid UserId,
    string FamilyName,
    string GivenName,
    string? AvatarUrl,
    bool IsOnline,
    DateTime? LastActiveAtUtc);
