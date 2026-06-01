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
        public const string Read = "admins:read";
        public const string Create = "admins:create";
        public const string Update = "admins:update";
        public const string Delete = "admins:delete";
        public const string AssignRole = "admins:assign-role";
    }

    public static class UserManagement
    {
        public const string Read = "users:read";
        public const string Create = "users:create";
        public const string Update = "users:update";
        public const string Delete = "users:delete";
    }

    public static class PostManagement
    {
        public const string Read = "posts:read";
        public const string Update = "posts:update";
        public const string Delete = "posts:delete";
    }

    public static class PostReportManagement
    {
        public const string Read = "post-reports:read";
        public const string Create = "post-reports:create";
        public const string Update = "post-reports:update";
        public const string Delete = "post-reports:delete";
    }

    public static class Statistics
    {
        public const string Read = "statistics:read";
    }

    public static class AdminAuditLogs
    {
        public const string Read = "admin-audit-logs:read";
    }

    public static class Media
    {
        public const string HardDelete = "media:hard-delete";
    }

    public static IReadOnlyCollection<PermissionDefinition> All { get; } =
    [
        new(PermissionManagement.Read, "Xem danh sach va chi tiet permission.", "Permission Management"),
        new(PermissionManagement.Create, "Tao permission moi.", "Permission Management"),
        new(PermissionManagement.Update, "Cap nhat permission.", "Permission Management"),
        new(PermissionManagement.Delete, "Xoa permission.", "Permission Management"),

        new(RoleManagement.Read, "Xem danh sach va chi tiet role.", "Role Management"),
        new(RoleManagement.Create, "Tao role moi.", "Role Management"),
        new(RoleManagement.Update, "Cap nhat role.", "Role Management"),
        new(RoleManagement.Delete, "Xoa role.", "Role Management"),
        new(RoleManagement.AssignPermission, "Bat/tat permission trong role.", "Role Management"),

        new(AdminManagement.Read, "Xem danh sach va chi tiet admin.", "Admin Management"),
        new(AdminManagement.Create, "Tao admin moi.", "Admin Management"),
        new(AdminManagement.Update, "Cap nhat admin.", "Admin Management"),
        new(AdminManagement.Delete, "Vo hieu hoa admin.", "Admin Management"),
        new(AdminManagement.AssignRole, "Gan role vao admin.", "Admin Management"),

        new(UserManagement.Read, "Xem danh sach va chi tiet user.", "User Management"),
        new(UserManagement.Create, "Tao user moi.", "User Management"),
        new(UserManagement.Update, "Cap nhat user.", "User Management"),
        new(UserManagement.Delete, "Vo hieu hoa user.", "User Management"),

        new(PostManagement.Read, "Xem danh sach va chi tiet post.", "Post Management"),
        new(PostManagement.Update, "Cap nhat post.", "Post Management"),
        new(PostManagement.Delete, "Soft delete post.", "Post Management"),

        new(PostReportManagement.Read, "Xem danh sach va chi tiet bao cao post.", "Post Report Management"),
        new(PostReportManagement.Create, "Tao bao cao post.", "Post Report Management"),
        new(PostReportManagement.Update, "Cap nhat trang thai bao cao post.", "Post Report Management"),
        new(PostReportManagement.Delete, "Xoa bao cao post.", "Post Report Management"),

        new(Statistics.Read, "Xem thong ke he thong.", "Statistics"),

        new(AdminAuditLogs.Read, "Xem lich su thao tac admin.", "Admin Audit Logs"),

        new(Media.HardDelete, "Xoa vinh vien media.", "Media")
    ];
}
