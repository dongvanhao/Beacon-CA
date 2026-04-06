namespace Beacon.Domain.Entities.Identity
{
    public class Admin
    {
        public int Id { get; set; }

        public string UserName { get; set; } = default!;
        public string PasswordHash { get; set; } = default!;

        public string? Name { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public ICollection<AdminRole> AdminRoles { get; set; } = new List<AdminRole>();
        public ICollection<RefreshTokenAdmin> RefreshTokens { get; set; } = new List<RefreshTokenAdmin>();
    }
}
