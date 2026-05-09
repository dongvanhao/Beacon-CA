namespace Beacon.Application.Features.Messaging.Dtos;

public record MessageGroupDetailDto(
    Guid GroupId,
    bool IsPrivate,
    DateTime CreatedAtUtc,
    string? DisplayName,
    string? DisplayAvatarUrl,
    IReadOnlyList<MessageGroupMemberDto> Members);
