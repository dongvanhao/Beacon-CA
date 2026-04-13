using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Beacon.Api.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    // Tên role được bypass toàn bộ permission check
    private const string SuperAdminRole = "SuperAdmin";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // SA bypass: nếu token mang role SuperAdmin → pass mọi API, không cần permission
        var isSuperAdmin = context.User.Claims
            .Any(c => c.Type == ClaimTypes.Role && c.Value == SuperAdminRole);

        if (isSuperAdmin)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Các role khác: kiểm tra permission bình thường
        var hasPermission = context.User.Claims
            .Any(c => c.Type == "permission" && c.Value == requirement.Permission);

        if (hasPermission)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
