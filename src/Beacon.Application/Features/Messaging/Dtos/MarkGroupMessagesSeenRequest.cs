namespace Beacon.Application.Features.Messaging.Dtos;

public record MarkGroupMessagesSeenRequest(Guid LastSeenMessageId);
