using Beacon.Domain.Common;

namespace Beacon.Domain.Entities.Identity
{
    public class Permission : BaseEntity
    {
        public string Name { get; private set; } = default!;       // e.g. "users:read"
        public string? Description { get; private set; }
        public string? Group { get; private set; }                  // e.g. "Users", "Safety"

        public ICollection<RolePermission> RolePermissions { get; private set; } = new List<RolePermission>();

        protected Permission() { }

        public static Permission Create(string name, string? description = null, string? group = null)
            => new() { Name = name, Description = description, Group = group };

        public void Update(string name, string? description = null, string? group = null)
        {
            Name = name;
            Description = description;
            Group = group;
        }
    }
}
