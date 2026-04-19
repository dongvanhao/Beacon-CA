using Beacon.Application.Features.Identity.Dtos;
using Beacon.Domain.Entities.Identity;

namespace Beacon.Application.Mappings.Identity;

public sealed class AdminAuthMapper
{
    public AdminAuthResponse ToAuthResponse(
        Admin admin,
        string accessToken,
        string refreshToken,
        DateTime accessTokenExpiresAt,
        IEnumerable<string> permissions)
        => new()
        {
            AdminId = admin.Id,
            Username = admin.Username,
            FullName = admin.FullName,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAt = accessTokenExpiresAt,
            Permissions = permissions
        };
}
