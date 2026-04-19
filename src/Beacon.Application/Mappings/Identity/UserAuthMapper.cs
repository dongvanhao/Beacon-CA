using Beacon.Application.Features.Identity.Dtos;
using Beacon.Domain.Entities.Identity;

namespace Beacon.Application.Mappings.Identity;

public sealed class UserAuthMapper
{
    public AuthResponse ToAuthResponse(
        User user,
        string accessToken,
        string refreshToken,
        DateTime accessTokenExpiresAt)
        => new()
        {
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAt = accessTokenExpiresAt
        };
}
