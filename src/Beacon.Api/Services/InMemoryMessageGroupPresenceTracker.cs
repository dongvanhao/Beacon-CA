using System.Collections.Concurrent;
using Beacon.Application.Common.Interfaces.IService;

namespace Beacon.Api.Services;

public sealed class InMemoryMessageGroupPresenceTracker : IMessageGroupPresenceTracker
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>>> _groupUsers = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, byte>> _connectionGroups = new();

    public bool IsUserInGroup(Guid userId, Guid groupId)
    {
        return _groupUsers.TryGetValue(groupId, out var users)
            && users.TryGetValue(userId, out var connections)
            && !connections.IsEmpty;
    }

    public void TrackJoin(Guid userId, Guid groupId, string connectionId)
    {
        var users = _groupUsers.GetOrAdd(
            groupId,
            _ => new ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>>());

        var connections = users.GetOrAdd(
            userId,
            _ => new ConcurrentDictionary<string, byte>());

        connections.TryAdd(connectionId, 0);

        var groups = _connectionGroups.GetOrAdd(
            connectionId,
            _ => new ConcurrentDictionary<Guid, byte>());

        groups.TryAdd(groupId, 0);
    }

    public void TrackLeave(Guid userId, Guid groupId, string connectionId)
    {
        RemoveFromGroup(userId, groupId, connectionId);

        if (_connectionGroups.TryGetValue(connectionId, out var groups))
        {
            groups.TryRemove(groupId, out _);
            if (groups.IsEmpty)
                _connectionGroups.TryRemove(connectionId, out _);
        }
    }

    public void TrackDisconnect(Guid userId, string connectionId)
    {
        if (!_connectionGroups.TryRemove(connectionId, out var groups))
            return;

        foreach (var groupId in groups.Keys)
            RemoveFromGroup(userId, groupId, connectionId);
    }

    private void RemoveFromGroup(Guid userId, Guid groupId, string connectionId)
    {
        if (!_groupUsers.TryGetValue(groupId, out var users))
            return;

        if (users.TryGetValue(userId, out var connections))
        {
            connections.TryRemove(connectionId, out _);
            if (connections.IsEmpty)
                users.TryRemove(userId, out _);
        }

        if (users.IsEmpty)
            _groupUsers.TryRemove(groupId, out _);
    }
}
