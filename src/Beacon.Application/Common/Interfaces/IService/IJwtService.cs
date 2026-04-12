using Beacon.Domain.Entities.Identity;

namespace Beacon.Application.Common.Interfaces.IService;

public interface IJwtService
{
    (string Token, DateTime ExpiresAt) GenerateAccessToken(User user);
    (string Token, DateTime ExpiresAt) GenerateRefreshToken();
}
