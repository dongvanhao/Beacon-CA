namespace Beacon.Application.Features.Messaging.Dtos;

public record MessageGroupMemberSettingDto(
    string? CustomName,
    bool IsMuted,
    Guid? LastReadMessageId,
    DateTime? LastReadAtUtc);
