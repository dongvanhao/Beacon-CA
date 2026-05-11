namespace Beacon.Application.Common.Interfaces.IService;

public record UserPresencePayload(
    Guid UserId,
    bool IsOnline,
    DateTime LastActiveAtUtc
    );
