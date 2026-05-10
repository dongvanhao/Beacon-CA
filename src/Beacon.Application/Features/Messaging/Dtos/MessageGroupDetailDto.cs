using Beacon.Domain.Enums.Messaging;

namespace Beacon.Application.Features.Messaging.Dtos;

public record MessageGroupDetailDto(
    Guid GroupId,
    MessageGroupType Type,
    DateTime CreatedAtUtc,
    string? DisplayName,
    string? DisplayAvatarUrl,
    IReadOnlyList<MessageGroupMemberDto> Members);
