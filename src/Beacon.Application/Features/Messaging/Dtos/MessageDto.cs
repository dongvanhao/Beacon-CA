namespace Beacon.Application.Features.Messaging.Dtos;

using System.Text.Json;
using Beacon.Domain.Enums.Messaging;

public record MessageDto(
    Guid Id,
    Guid GroupId,
    Guid SenderId,
    string SenderDisplayName,
    string Content,
    MessageType Type,
    JsonElement? Metadata,
    DateTime CreatedAtUtc,
    Guid? PostId,
    MessagePostDto? Post);
