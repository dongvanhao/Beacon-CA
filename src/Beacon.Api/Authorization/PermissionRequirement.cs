using Microsoft.AspNetCore.Authorization;

namespace Beacon.Api.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
