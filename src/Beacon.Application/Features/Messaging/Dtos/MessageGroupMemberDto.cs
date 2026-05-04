namespace Beacon.Application.Features.Messaging.Dtos;

public record MessageGroupMemberDto(
    Guid UserId,
    string Username,
    string FamilyName,
    string GivenName,
    string? AvatarUrl);
