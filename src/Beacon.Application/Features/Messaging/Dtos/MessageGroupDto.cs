using Beacon.Domain.Enums.Messaging;

namespace Beacon.Application.Features.Messaging.Dtos;

public record MessageGroupDto(
    Guid GroupId,
    MessageGroupType Type,
    Guid? PeerUserId,
    DateTime CreatedAtUtc,
    Guid? LastMessageId,
    string? LastMessageContent,
    DateTime? LastMessageAtUtc,
    string? LastMessageSenderFamilyName,
    string? LastMessageSenderGivenName,
    Guid? LastSeenMessageId,
    bool IsSeenLatest,
    int UnreadCount,
    string? DisplayName,
    string? DisplayAvatarUrl);
