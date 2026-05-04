namespace Beacon.Application.Features.Messaging.Dtos;

public record MessageDto(
    Guid Id,
    Guid GroupId,
    Guid SenderId,
    string SenderUsername,
    string Content,
    DateTime CreatedAtUtc);
