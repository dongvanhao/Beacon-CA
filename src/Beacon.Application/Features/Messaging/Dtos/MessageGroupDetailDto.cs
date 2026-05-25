using Beacon.Domain.Enums.Messaging;

namespace Beacon.Application.Features.Messaging.Dtos;

public record MessageGroupDetailDto(
    Guid GroupId,
    MessageGroupType Type,
    DateTime CreatedAtUtc,
    bool RequireApprovalToAddMembers,
    string? DisplayName,
    string? DisplayAvatarUrl,
    MessageGroupMemberSettingDto Setting,
    IReadOnlyList<MessageGroupMemberDto> Members);
