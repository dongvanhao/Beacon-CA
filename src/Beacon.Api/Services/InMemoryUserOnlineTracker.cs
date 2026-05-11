using System.Collections.Concurrent;
using Beacon.Application.Common.Interfaces.IService;

namespace Beacon.Api.Services;

public sealed class InMemoryUserOnlineTracker : IUserOnlineTracker
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _userConnections = new();
    private readonly ConcurrentDictionary<string, Guid> _connectionUsers = new();

    public bool IsOnline(Guid userId)
    {
        return _userConnections.TryGetValue(userId, out var connections)
            && !connections.IsEmpty;
    }

    public void TrackOnline(Guid userId, string connectionId)
    {
        var connections = _userConnections.GetOrAdd(
            userId,
            _ => new ConcurrentDictionary<string, byte>());

        connections.TryAdd(connectionId, 0);
        _connectionUsers[connectionId] = userId;
    }

    public void TrackOffline(Guid userId, string connectionId)
    {
        if (_userConnections.TryGetValue(userId, out var connections))
        {
            connections.TryRemove(connectionId, out _);
            if (connections.IsEmpty)
                _userConnections.TryRemove(userId, out _);
        }

        _connectionUsers.TryRemove(connectionId, out _);
    }
}
