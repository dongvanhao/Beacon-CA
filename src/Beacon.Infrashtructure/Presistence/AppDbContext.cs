using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Beacon.Infrashtructure.Presistence
{
    public class AppDbContext : DbContext
    {
        // 🔥 Identity DbSets
        public DbSet<Admin> Admins => Set<Admin>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
        public DbSet<AdminRole> AdminRoles => Set<AdminRole>();
        public DbSet<RefreshTokenAdmin> RefreshTokenAdmins => Set<RefreshTokenAdmin>();

        // User module DbSets
        public DbSet<User> Users => Set<User>();
        public DbSet<UserSetting> UserSettings => Set<UserSetting>();
        public DbSet<UserRefreshToken> RefreshTokens => Set<UserRefreshToken>();
        public DbSet<Media> Media => Set<Media>();

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 🔥 Default schema (rất nên có khi project lớn)
            modelBuilder.HasDefaultSchema("identity");

            // 🔥 Apply tất cả config từ assembly
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

            // 🔥 Global convention (optional nhưng xịn)
            // modelBuilder.UseSnakeCaseNamingConvention();

            base.OnModelCreating(modelBuilder);
        }

        // 🔥 Audit tự động (CreatedAt / UpdatedAt)
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplyAuditing();
            return await base.SaveChangesAsync(cancellationToken);
        }

        public override int SaveChanges()
        {
            ApplyAuditing();
            return base.SaveChanges();
        }

        private void ApplyAuditing()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is IAuditableEntity &&
                           (e.State == EntityState.Added || e.State == EntityState.Modified));

            foreach (var entry in entries)
            {
                var entity = (IAuditableEntity)entry.Entity;

                if (entry.State == EntityState.Added)
                {
                    entity.CreatedAt = DateTime.UtcNow;
                }

                entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    // 🔥 Interface để đánh dấu entity cần audit
    public interface IAuditableEntity
    {
        DateTime CreatedAt { get; set; }
        DateTime UpdatedAt { get; set; }
    }
}