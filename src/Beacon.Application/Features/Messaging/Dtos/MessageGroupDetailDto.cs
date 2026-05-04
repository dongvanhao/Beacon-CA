namespace Beacon.Application.Features.Messaging.Dtos;

public record MessageGroupDetailDto(
    Guid GroupId,
    bool IsPrivate,
    DateTime CreatedAtUtc,
    IReadOnlyList<MessageGroupMemberDto> Members);
