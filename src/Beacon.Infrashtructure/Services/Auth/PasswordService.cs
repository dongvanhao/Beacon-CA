using Beacon.Application.Common.Interfaces.IService;

namespace Beacon.Infrashtructure.Services.Auth
{
    public class PasswordService : IPasswordService
    {
        public bool Verify(string rawPassword, string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(rawPassword) || string.IsNullOrWhiteSpace(passwordHash))
            {
                return false;
            }

            // Supports bcrypt hashes and keeps backward compatibility for plain-text seeded data.
            if (passwordHash.StartsWith("$2", StringComparison.Ordinal))
            {
                return global::BCrypt.Net.BCrypt.Verify(rawPassword, passwordHash);
            }

            return string.Equals(rawPassword, passwordHash, StringComparison.Ordinal);
        }
    }
}
