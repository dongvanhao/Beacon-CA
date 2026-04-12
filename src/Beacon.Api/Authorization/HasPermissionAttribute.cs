using Microsoft.AspNetCore.Authorization;

namespace Beacon.Api.Authorization;

public class HasPermissionAttribute(string permission) : AuthorizeAttribute(policy: permission)
{
}
