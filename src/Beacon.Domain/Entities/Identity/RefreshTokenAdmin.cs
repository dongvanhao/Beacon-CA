namespace Beacon.Domain.Entities.Identity
{
    public class RefreshTokenAdmin
    {
        public int Id { get; set; }

        public int AdminId { get; set; }

        public string TokenHash { get; set; } = default!;
        public DateTime ExpiresAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public bool Revoked { get; set; } = false;
        public string? UserAgent { get; set; }
        public string? IpAddress { get; set; }

        public Admin Admin { get; set; } = default!;
    }
}
