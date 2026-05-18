namespace Beacon.Application.Common.Interfaces.IService;

public interface IUserPresenceService
{
    Task MarkOnlineAsync(Guid userId, CancellationToken ct = default);

    Task MarkOfflineAsync(Guid userId, CancellationToken ct = default);
}
