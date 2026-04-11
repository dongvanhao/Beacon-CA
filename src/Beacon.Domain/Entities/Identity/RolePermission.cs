namespace Beacon.Domain.Entities.Identity
{
    // Junction table: Role <-> Permission (many-to-many)
    public class RolePermission
    {
        public Guid RoleId { get; private set; }
        public Guid PermissionId { get; private set; }

        public Role Role { get; private set; } = default!;
        public Permission Permission { get; private set; } = default!;

        protected RolePermission() { }

        public static RolePermission Create(Guid roleId, Guid permissionId)
            => new() { RoleId = roleId, PermissionId = permissionId };
    }
}
