using Beacon.Domain.Entities.Identity;
using DomainUser = Beacon.Domain.Entities.User.User;

namespace Beacon.Application.Common.Interfaces.IService
{
    public interface IJwtService
    {
        string GenerateAccessTokenForAdmin(Admin admin);
        string GenerateAccessTokenForUser(DomainUser user);
        (string RawToken, string TokenHash, DateTime ExpiresAtUtc) GenerateRefreshToken();
    }
}
