namespace Beacon.Application.Common.Interfaces.IService;

public interface IMessageGroupPresenceTracker
{
    bool IsUserInGroup(Guid userId, Guid groupId);

    void TrackJoin(Guid userId, Guid groupId, string connectionId);

    void TrackLeave(Guid userId, Guid groupId, string connectionId);

    void TrackDisconnect(Guid userId, string connectionId);
}
