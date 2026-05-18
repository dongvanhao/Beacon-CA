using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Group;
using Microsoft.Extensions.Logging;

namespace Beacon.Infrashtructure.Services;

public class UserPresenceService(
    IUserRepository userRepo,
    IFriendRepository friendRepo,
    IRealtimeNotifier notifier,
    ILogger<UserPresenceService> logger) : IUserPresenceService
{
    public async Task MarkOnlineAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userRepo.GetByIdAsync(userId, ct);
        if (user is null)
        {
            logger.LogWarning("Presence update skipped: user not found. UserId={UserId}", userId);
            return;
        }

        user.RecordActivity();
        await userRepo.SaveChangesAsync(ct);

        var friendIds = await friendRepo.ListFriendIdsAsync(userId, ct);
        if (friendIds.Count == 0) return;

        var payload = new UserPresencePayload(userId, true, user.LastActiveAtUtc ?? DateTime.UtcNow);

        var tasks = friendIds
            .Distinct()
            .Select(async friendId =>
            {
                try
                {
                    await notifier.NotifyUserPresenceAsync(friendId, payload, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Presence notify failed for user {UserId}", friendId);
                }
            });

        await Task.WhenAll(tasks);
    }

    public async Task MarkOfflineAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userRepo.GetByIdAsync(userId, ct);
        if (user is null)
        {
            logger.LogWarning("Presence update skipped: user not found. UserId={UserId}", userId);
            return;
        }

        user.RecordActivity();
        await userRepo.SaveChangesAsync(ct);

        var friendIds = await friendRepo.ListFriendIdsAsync(userId, ct);
        if (friendIds.Count == 0) return;

        var payload = new UserPresencePayload(userId, false, user.LastActiveAtUtc ?? DateTime.UtcNow);

        var tasks = friendIds
            .Distinct()
            .Select(async friendId =>
            {
                try
                {
                    await notifier.NotifyUserPresenceAsync(friendId, payload, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Presence notify failed for user {UserId}", friendId);
                }
            });

        await Task.WhenAll(tasks);
    }
}
