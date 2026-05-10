using Beacon.Domain.Enums.Messaging;

namespace Beacon.Application.Features.Messaging.Dtos;

public record MessageGroupMemberDto(
    Guid UserId,
    string FamilyName,
    string GivenName,
    string? AvatarUrl,
    GroupMemberRole Role);
