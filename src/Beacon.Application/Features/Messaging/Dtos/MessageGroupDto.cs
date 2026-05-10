using Beacon.Domain.Enums.Messaging;

namespace Beacon.Application.Features.Messaging.Dtos;

public record MessageGroupDto(
    Guid GroupId,
    MessageGroupType Type,
    DateTime CreatedAtUtc,
    string? LastMessageContent,
    DateTime? LastMessageAtUtc,
    string? LastMessageSenderFamilyName,
    string? LastMessageSenderGivenName,
    string? DisplayName,
    string? DisplayAvatarUrl);
