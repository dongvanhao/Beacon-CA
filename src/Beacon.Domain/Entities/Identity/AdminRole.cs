namespace Beacon.Domain.Entities.Identity
{
    public class AdminRole
    {
        public int Id { get; set; }

        public int AdminId { get; set; }
        public int RoleId { get; set; }

        public Admin Admin { get; set; } = default!;
        public Role Role { get; set; } = default!;
    }
}
