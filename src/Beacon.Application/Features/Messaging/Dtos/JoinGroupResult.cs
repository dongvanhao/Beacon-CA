namespace Beacon.Application.Features.Messaging.Dtos;

public record JoinGroupResult(
    bool Success,
    Guid MessageGroupId,
    string? Room,
    string? ErrorMessage);
