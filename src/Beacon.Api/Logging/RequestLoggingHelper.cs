using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Serilog.Events;

namespace Beacon.Api.Logging;

/// <summary>
/// Logic thuần (không phụ thuộc Serilog runtime / HttpContext pipeline) cho request logging.
/// Tách riêng để unit-test trực tiếp — xem <c>RequestLoggingHelperTests</c>.
/// </summary>
public static class RequestLoggingHelper
{
    private static readonly string[] ExcludedPrefixes = ["/health", "/hangfire"];

    // Khớp EXACT tên param (case-insensitive), KHÔNG substring (tránh over-match "monkey" ⊃ "key").
    private static readonly HashSet<string> SensitiveParams =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "token", "access_token", "refresh_token",
            "key", "apikey", "secret", "password"
        };

    private const string MaskedValue = "***";

    /// <summary>Path thuộc nhóm noise (/health*, /hangfire*) → log ở Verbose để bị MinimumLevel lọc.</summary>
    public static bool IsExcluded(PathString path) =>
        ExcludedPrefixes.Any(p =>
            path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>UserId (GUID) từ <c>NameIdentifier</c>, fallback <c>sub</c>, mặc định <c>anonymous</c>. KHÔNG log username/email.</summary>
    public static string ResolveUserId(ClaimsPrincipal? user) =>
        user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? user?.FindFirst("sub")?.Value
        ?? "anonymous";

    /// <summary>Mask value của param nhạy cảm (exact-name, case-insensitive). Trả query string đã sanitize.</summary>
    public static string SanitizeQueryString(IQueryCollection query)
    {
        var sb = new StringBuilder();
        var first = true;

        foreach (var kvp in query)
        {
            sb.Append(first ? '?' : '&');
            first = false;

            sb.Append(kvp.Key).Append('=');
            sb.Append(SensitiveParams.Contains(kvp.Key) ? MaskedValue : kvp.Value.ToString());
        }

        return sb.ToString();
    }

    /// <summary>Khi <paramref name="mask"/> bật: mask octet cuối IPv4 / nhóm cuối IPv6. Null → null.</summary>
    public static string? MaskIp(IPAddress? ip, bool mask)
    {
        if (ip is null)
            return null;

        if (!mask)
            return ip.ToString();

        return ip.AddressFamily switch
        {
            AddressFamily.InterNetwork => MaskIpv4(ip),
            AddressFamily.InterNetworkV6 => MaskIpv6(ip),
            _ => ip.ToString()
        };
    }

    private static string MaskIpv4(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        bytes[3] = 0;
        return new IPAddress(bytes).ToString();
    }

    private static string MaskIpv6(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        bytes[14] = 0;
        bytes[15] = 0;
        return new IPAddress(bytes).ToString();
    }

    /// <summary>
    /// Map kết quả request → level. Tách rời <c>HttpContext</c> để test trực tiếp.
    /// exception/5xx → Error · 4xx → Warning · excluded → Verbose · còn lại → Information.
    /// </summary>
    public static LogEventLevel ResolveLevel(int statusCode, bool isExcluded, bool hasException)
    {
        if (isExcluded)
            return LogEventLevel.Verbose;

        if (hasException || statusCode >= 500)
            return LogEventLevel.Error;

        if (statusCode >= 400)
            return LogEventLevel.Warning;

        return LogEventLevel.Information;
    }
}
