namespace Beacon.Application.Common.Interfaces.IService;

public interface IFcmService
{
    bool IsAvailable { get; }

    Task SendToTokenAsync(
        string token,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default);

    /// <returns>true nếu FCM available và có ít nhất 1 token active (send đã được attempt); false nếu không.</returns>
    Task<bool> SendToUserAsync(
        Guid userId,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<string>> SendToUserAndGetInvalidTokensAsync(
        Guid userId,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default);
}
