namespace Beacon.Application.Common.Interfaces.IService;

public interface IFcmService
{
    Task SendToTokenAsync(
        string token,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default);

    Task SendToUserAsync(
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
