namespace Beacon.Domain.Entities.Identity
{
    // Junction table: Admin <-> Role (many-to-many)
    public class AdminRole
    {
        public Guid AdminId { get; private set; }
        public Guid RoleId { get; private set; }
        public DateTime AssignedAtUtc { get; private set; }

        public Admin Admin { get; private set; } = default!;
        public Role Role { get; private set; } = default!;

        protected AdminRole() { }

        public static AdminRole Create(Guid adminId, Guid roleId)
            => new() { AdminId = adminId, RoleId = roleId, AssignedAtUtc = DateTime.UtcNow };
    }
}
