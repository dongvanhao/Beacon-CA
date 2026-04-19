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
            Email = user.Email,
            FamilyName = user.FamilyName,
            GivenName = user.GivenName,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAt = accessTokenExpiresAt
        };
}
