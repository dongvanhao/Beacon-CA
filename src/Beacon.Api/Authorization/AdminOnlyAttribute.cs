using Microsoft.AspNetCore.Authorization;

namespace Beacon.Api.Authorization;

/// <summary>
/// Chỉ cho phép token có claim "actor" = "admin".
/// Dùng cho tất cả Admin endpoint thay vì [Authorize] thuần.
/// </summary>
public class AdminOnlyAttribute() : AuthorizeAttribute(policy: "AdminOnly")
{
}
