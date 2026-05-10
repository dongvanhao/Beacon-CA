using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.IRepository.Identity;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;

namespace Beacon.Infrashtructure.Services;

public class FcmService(
    IUserDeviceTokenRepository tokenRepo,
    ILogger<FcmService> logger) : IFcmService
{
    public async Task SendToTokenAsync(
        string token,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default)
    {
        // Firebase chưa được cấu hình (FIREBASE_CREDENTIAL_JSON trống) → skip nhẹ nhàng
        if (FirebaseApp.DefaultInstance is null)
        {
            logger.LogDebug("FCM skipped — Firebase not configured. Token={Token}", MaskToken(token));
            return;
        }

        var message = new Message
        {
            Token = token,
            Notification = new Notification { Title = title, Body = body },
            Data = data ?? new Dictionary<string, string>()
        };

        try
        {
            await FirebaseMessaging.DefaultInstance.SendAsync(message, ct);
        }
        catch (FirebaseMessagingException ex)
        {
            logger.LogWarning("FCM send failed: token={Token}, error={Error}",
                MaskToken(token), ex.MessagingErrorCode);
            throw;
        }
    }

    public async Task<IReadOnlyList<string>> SendToUserAndGetInvalidTokensAsync(
        Guid userId,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default)
    {
        // Firebase chưa được cấu hình → skip, không throw, không ảnh hưởng SignalR
        if (FirebaseApp.DefaultInstance is null)
        {
            logger.LogDebug("FCM skipped — Firebase not configured. UserId={UserId}", userId);
            return Array.Empty<string>();
        }

        var tokens = await tokenRepo.GetActiveByUserIdAsync(userId, ct);
        if (tokens.Count == 0) return Array.Empty<string>();

        var messages = tokens.Select(t => new Message
        {
            Token = t.Token,
            Notification = new Notification { Title = title, Body = body },
            Data = data ?? new Dictionary<string, string>()
        }).ToList();

        var response = await FirebaseMessaging.DefaultInstance.SendEachAsync(messages, ct);

        var invalidTokens = new List<string>();
        for (var i = 0; i < response.Responses.Count; i++)
        {
            var r = response.Responses[i];
            if (!r.IsSuccess && IsTokenInvalid(r.Exception))
            {
                invalidTokens.Add(tokens[i].Token);
                logger.LogWarning("FCM token invalid for userId={UserId}", userId);
            }
        }

        return invalidTokens;
    }

    private static bool IsTokenInvalid(FirebaseMessagingException? ex)
        => ex?.MessagingErrorCode is MessagingErrorCode.Unregistered
            or MessagingErrorCode.InvalidArgument;

    private static string MaskToken(string token)
        => token.Length > 10 ? token[..6] + "..." + token[^4..] : "***";
}
