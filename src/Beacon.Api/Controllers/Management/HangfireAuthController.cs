using System.Security.Claims;
using Beacon.Api.Authorization;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers.Management;

[Route("api/v1/admin/hangfire-auth")]
[AdminOnly]
public class HangfireAuthController(IDataProtectionProvider dataProtectionProvider) : BaseController
{
    private const string CookieName = "beacon_hf_auth";
    private const string Purpose = "Beacon.Hangfire.Dashboard";
    private static readonly TimeSpan CookieTtl = TimeSpan.FromHours(1);

    #region
    /// <summary>Cấp quyền truy cập Hangfire dashboard qua cookie.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;admin-token&gt;</c> với role <c>SuperAdmin</c>.
    ///
    /// Sau khi gọi endpoint này thành công, browser sẽ có cookie <c>beacon_hf_auth</c> (HttpOnly, TTL 1h).
    /// Mở <c>/hangfire</c> trong cùng browser để vào dashboard.
    ///
    /// Các giá trị <c>code</c>:
    /// - <c>null</c>: Thành công, cookie đã được set.
    /// - <c>HANGFIRE_FORBIDDEN</c>: Không phải SuperAdmin.
    ///
    /// Format: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpPost]
    public IActionResult Authenticate()
    {
        if (!User.IsInRole("SuperAdmin"))
            return HandleResult(Result.Failure(
                Error.Forbidden(ErrorCodes.Hangfire.HANGFIRE_FORBIDDEN,
                    "Chỉ SuperAdmin mới được truy cập Hangfire dashboard.")));

        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        var expiry = DateTimeOffset.UtcNow.Add(CookieTtl);
        var payload = $"{adminId}|{expiry:O}";

        var token = dataProtectionProvider
            .CreateProtector(Purpose)
            .Protect(payload);

        Response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = expiry
        });

        return HandleResult(Result.Success(), "Hangfire dashboard access granted. Navigate to /hangfire.");
    }

    #region
    /// <summary>Thu hồi quyền truy cập Hangfire dashboard.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;admin-token&gt;</c>.
    ///
    /// Xóa cookie <c>beacon_hf_auth</c> khỏi browser.
    ///
    /// Các giá trị <c>code</c>:
    /// - <c>null</c>: Thành công.
    ///
    /// Format: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpDelete]
    public IActionResult Revoke()
    {
        Response.Cookies.Delete(CookieName);
        return HandleResult(Result.Success(), "Hangfire dashboard access revoked.");
    }
}
