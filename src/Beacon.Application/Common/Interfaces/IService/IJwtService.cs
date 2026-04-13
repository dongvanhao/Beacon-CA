using Beacon.Domain.Entities.Identity;

namespace Beacon.Application.Common.Interfaces.IService;

public interface IJwtService
{
    (string Token, DateTime ExpiresAt) GenerateAccessToken(User user, Guid deviceId);
    (string Token, DateTime ExpiresAt) GenerateAdminAccessToken(Admin admin, IEnumerable<string> permissions);
    (string Token, DateTime ExpiresAt) GenerateRefreshToken();
}
