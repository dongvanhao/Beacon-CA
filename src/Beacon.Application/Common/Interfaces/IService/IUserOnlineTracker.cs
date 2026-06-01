namespace Beacon.Application.Common.Interfaces.IService;

public interface IUserOnlineTracker
{
    int OnlineUserCount { get; }

    bool IsOnline(Guid userId);

    void TrackOnline(Guid userId, string connectionId);

    void TrackOffline(Guid userId, string connectionId);
}
