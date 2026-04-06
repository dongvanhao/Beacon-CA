using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Common.Options;
using Beacon.Domain.Entities.Identity;
using Beacon.Shared.Common;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using DomainUser = Beacon.Domain.Entities.User.User;

namespace Beacon.Infrashtructure.Services.Auth
{
    public class JwtService : IJwtService
    {
        private readonly JwtOptions _jwtOptions;

        public JwtService(IOptions<JwtOptions> jwtOptions)
        {
            _jwtOptions = jwtOptions.Value;
        }

        public string GenerateAccessTokenForAdmin(Admin admin)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
                new(JwtRegisteredClaimNames.UniqueName, admin.UserName),
                new(ClaimTypes.NameIdentifier, admin.Id.ToString()),
                new(ClaimTypes.Name, admin.UserName),
                new(ClaimTypes.Role, SystemRoles.Admin)
            };

            return GenerateAccessToken(claims);
        }

        public string GenerateAccessTokenForUser(DomainUser user)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.UniqueName, user.UserName),
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.UserName),
                new(ClaimTypes.Role, SystemRoles.User)
            };

            return GenerateAccessToken(claims);
        }

        public (string RawToken, string TokenHash, DateTime ExpiresAtUtc) GenerateRefreshToken()
        {
            var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            var tokenHash = ComputeSha256Hash(rawToken);
            var expiresAtUtc = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays);

            return (rawToken, tokenHash, expiresAtUtc);
        }

        private string GenerateAccessToken(IEnumerable<Claim> claims)
        {
            ValidateJwtOptions();

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Issuer = _jwtOptions.Issuer,
                Audience = _jwtOptions.Audience,
                Expires = expires,
                SigningCredentials = credentials
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private string ComputeSha256Hash(string value)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(hash);
        }

        private void ValidateJwtOptions()
        {
            if (string.IsNullOrWhiteSpace(_jwtOptions.Issuer)
                || string.IsNullOrWhiteSpace(_jwtOptions.Audience)
                || string.IsNullOrWhiteSpace(_jwtOptions.SecretKey))
            {
                throw new InvalidOperationException("Jwt configuration is invalid.");
            }

            if (_jwtOptions.SecretKey.Length < 32)
            {
                throw new InvalidOperationException("Jwt:SecretKey must be at least 32 characters.");
            }
        }
    }
}
