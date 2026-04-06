namespace Beacon.Domain.Entities.Identity
{
    public class Permission
    {
        public int Id { get; set; }

        public string Code { get; set; } = default!;
        public string? Description { get; set; }

        public DateTime? CreatedAt { get; set; }

        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}
