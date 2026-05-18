namespace Beacon.Application.Features.Messaging.Dtos;

public record MessageDto(
    Guid Id,
    Guid GroupId,
    Guid SenderId,
    string SenderFamilyName,
    string SenderGivenName,
    string Content,
    DateTime CreatedAtUtc,
    Guid? PostId,
    MessagePostDto? Post);
