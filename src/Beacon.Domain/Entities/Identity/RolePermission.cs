namespace Beacon.Domain.Entities.Identity
{
    public class RolePermission
    {
        public int Id { get; set; }

        public int RoleId { get; set; }
        public int PermissionId { get; set; }

        public Role Role { get; set; } = default!;
        public Permission Permission { get; set; } = default!;
    }
}
