namespace Beacon.Domain.Entities.User
{
    public class UserRefreshToken
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string TokenHash { get; set; } = default!;
        public DateTime ExpiresAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public bool Revoked { get; set; } = false;
        public string? UserAgent { get; set; }
        public string? IpAddress { get; set; }

        public User User { get; set; } = default!;
    }
}
