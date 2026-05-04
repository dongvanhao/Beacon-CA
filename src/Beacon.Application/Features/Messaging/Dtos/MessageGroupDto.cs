namespace Beacon.Application.Features.Messaging.Dtos;

public record MessageGroupDto(
    Guid GroupId,
    bool IsPrivate,
    DateTime CreatedAtUtc,
    string? LastMessageContent,
    DateTime? LastMessageAtUtc,
    string? LastMessageSenderUsername);
