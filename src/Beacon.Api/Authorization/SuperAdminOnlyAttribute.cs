using Microsoft.AspNetCore.Authorization;

namespace Beacon.Api.Authorization;

/// <summary>
/// Chỉ cho phép admin token có role SuperAdmin.
/// </summary>
public class SuperAdminOnlyAttribute() : AuthorizeAttribute(policy: "SuperAdminOnly")
{
}
