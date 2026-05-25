using Beacon.Domain.Common;
using Beacon.Domain.Entities.Checkins;
using Beacon.Domain.Entities.Group;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Entities.Notification;
using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Entities.Safety;
using Beacon.Domain.Entities.Setting;
using Beacon.Domain.Entities.Storage;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Infrashtructure.Presistence
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<UserDevice> UserDevices => Set<UserDevice>();
        public DbSet<UserDeviceToken> UserDeviceTokens => Set<UserDeviceToken>();

        public DbSet<Admin> Admins => Set<Admin>();
        public DbSet<AdminRole> AdminRoles => Set<AdminRole>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<RefreshTokenAdmin> RefreshTokenAdmins => Set<RefreshTokenAdmin>();

        public DbSet<SafetySetting> SafetySettings => Set<SafetySetting>();

        // === CÁC BẢNG TÍNH NĂNG CHƯA LÀM TỚI (Tạm ẩn) ===
        // public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
        // public DbSet<AppPreference> AppPreferences => Set<AppPreference>();

        public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
        public DbSet<DailySafetyRecord> DailySafetyRecords => Set<DailySafetyRecord>();

        public DbSet<MediaObject> MediaObjects => Set<MediaObject>();
        public DbSet<Post> Posts => Set<Post>();
        public DbSet<PostReaction> PostReactions => Set<PostReaction>();

        public DbSet<Checkin> Checkins => Set<Checkin>();
        public DbSet<CheckinMedia> CheckinMedias => Set<CheckinMedia>();

        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<Friend> Friends => Set<Friend>();
        public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();
        public DbSet<MessageGroup> MessageGroups => Set<MessageGroup>();
        public DbSet<MessageGroupMember> MessageGroupMembers => Set<MessageGroupMember>();
        public DbSet<MessageGroupMemberSetting> MessageGroupMemberSettings => Set<MessageGroupMemberSetting>();
        public DbSet<Message> Messages => Set<Message>();

        public DbSet<AlertIncident> AlertIncidents => Set<AlertIncident>();
        // public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
                entry.Property(nameof(AuditableEntity.CreatedAtUtc)).CurrentValue = now;

            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Property(nameof(AuditableEntity.UpdatedAtUtc)).CurrentValue = now;
        }
            return await base.SaveChangesAsync(cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

            modelBuilder.Entity<UserDevice>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<MediaObject>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<Post>().HasQueryFilter(p => p.DeletedAtUtc == null);
            modelBuilder.Entity<MessageGroup>().HasQueryFilter(x => !x.IsDeleted);

            // Dependent entities phải có filter tương ứng với required-end đã có filter,
            // tránh EF10622 warning và kết quả bất ngờ khi parent bị soft-delete.
            modelBuilder.Entity<Message>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<MessageGroupMember>().HasQueryFilter(m => !m.Group.IsDeleted);
            modelBuilder.Entity<CheckinMedia>().HasQueryFilter(cm => !cm.MediaObject.IsDeleted);
            modelBuilder.Entity<EmergencyContact>().HasQueryFilter(e => !e.IsDeleted);

            base.OnModelCreating(modelBuilder);
        }
    }
}
