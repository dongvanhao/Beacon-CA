namespace Beacon.Shared.Constants;

public sealed record PermissionDefinition(string Name, string Description, string Group);

public static class PermissionCodes
{
    public static class PermissionManagement
    {
        public const string Read = "permissions:read";
        public const string Create = "permissions:create";
        public const string Update = "permissions:update";
        public const string Delete = "permissions:delete";
    }

    public static class RoleManagement
    {
        public const string Read = "roles:read";
        public const string Create = "roles:create";
        public const string Update = "roles:update";
        public const string Delete = "roles:delete";
        public const string AssignPermission = "roles:assign-permission";
    }

    public static class AdminManagement
    {
        public const string AssignRole = "admins:assign-role";
    }

    public static class Media
    {
        public const string HardDelete = "media:hard-delete";
    }

    public static IReadOnlyCollection<PermissionDefinition> All { get; } =
    [
        new(PermissionManagement.Read, "Xem danh sách và chi tiết permission.", "Permission Management"),
        new(PermissionManagement.Create, "Tạo permission mới.", "Permission Management"),
        new(PermissionManagement.Update, "Cập nhật permission.", "Permission Management"),
        new(PermissionManagement.Delete, "Xóa permission.", "Permission Management"),

        new(RoleManagement.Read, "Xem danh sách và chi tiết role.", "Role Management"),
        new(RoleManagement.Create, "Tạo role mới.", "Role Management"),
        new(RoleManagement.Update, "Cập nhật role.", "Role Management"),
        new(RoleManagement.Delete, "Xóa role.", "Role Management"),
        new(RoleManagement.AssignPermission, "Gắn permission vào role.", "Role Management"),

        new(AdminManagement.AssignRole, "Gắn role vào admin.", "Admin Management"),

        new(Media.HardDelete, "Xóa vĩnh viễn media.", "Media")
    ];
}
