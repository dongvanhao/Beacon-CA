namespace Beacon.Application.Features.Messaging.Dtos;

public record TypingStatusRequest(Guid MessageGroupId, bool IsTyping);
