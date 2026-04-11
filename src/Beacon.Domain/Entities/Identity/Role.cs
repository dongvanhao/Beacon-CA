using Beacon.Domain.Common;

namespace Beacon.Domain.Entities.Identity
{
    public class Role : AuditableEntity
    {
        public string Name { get; private set; } = default!;       // e.g. "SuperAdmin", "Moderator"
        public string? Description { get; private set; }
        public bool IsActive { get; private set; } = true;

        public ICollection<AdminRole> AdminRoles { get; private set; } = new List<AdminRole>();
        public ICollection<RolePermission> RolePermissions { get; private set; } = new List<RolePermission>();

        protected Role() { }

        public static Role Create(string name, string? description = null)
            => new() { Name = name, Description = description };

        public void Deactivate() => IsActive = false;
        public void Activate() => IsActive = true;
    }
}
