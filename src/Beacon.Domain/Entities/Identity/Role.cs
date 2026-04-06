namespace Beacon.Domain.Entities.Identity
{
    public class Role
    {
        public int Id { get; set; }

        public string Name { get; set; } = default!;
        public string? Description { get; set; }

        public DateTime? CreatedAt { get; set; }

        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
        public ICollection<AdminRole> AdminRoles { get; set; } = new List<AdminRole>();
    }
}
