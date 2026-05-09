namespace Beacon.Application.Features.Messaging.Dtos;

public record SendMessageRequest(string Content, string? ClientMessageId);
