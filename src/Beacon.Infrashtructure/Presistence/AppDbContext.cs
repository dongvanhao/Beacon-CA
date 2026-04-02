using Beacon.Domain.Entities.Checkins;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Notification;
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

        public DbSet<SafetySetting> SafetySettings => Set<SafetySetting>();
        public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
        public DbSet<AppPreference> AppPreferences => Set<AppPreference>();

        public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
        public DbSet<DailySafetyRecord> DailySafetyRecords => Set<DailySafetyRecord>();

        public DbSet<MediaObject> MediaObjects => Set<MediaObject>();

        public DbSet<Checkin> Checkins => Set<Checkin>();
        public DbSet<CheckinMedia> CheckinMedias => Set<CheckinMedia>();

        public DbSet<AlertIncident> AlertIncidents => Set<AlertIncident>();
        public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

            modelBuilder.Entity<UserDevice>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<EmergencyContact>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<MediaObject>().HasQueryFilter(x => !x.IsDeleted);

            base.OnModelCreating(modelBuilder);
        }
    }
}
