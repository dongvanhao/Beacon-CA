using Beacon.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrashtructure.Presistence.Configuration.User
{
    public class UserSettingConfiguration : IEntityTypeConfiguration<UserSetting>
    {
        public void Configure(EntityTypeBuilder<UserSetting> builder)
        {
            builder.ToTable("user_settings", "user");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.UserId)
                .HasColumnName("user_id");

            builder.Property(x => x.Theme)
                .HasMaxLength(20)
                .HasDefaultValue("light")
                .HasColumnName("theme");

            builder.Property(x => x.NotifyMessage)
                .HasDefaultValue(true)
                .HasColumnName("notify_message");

            builder.Property(x => x.NotifyHealth)
                .HasDefaultValue(true)
                .HasColumnName("notify_health");

            builder.Property(x => x.HealthReminderEnabled)
                .HasDefaultValue(false)
                .HasColumnName("health_reminder_enabled");

            builder.Property(x => x.HealthReminderTime)
                .HasColumnType("time")
                .HasColumnName("health_reminder_time");

            builder.Property(x => x.HealthInactiveThreshold)
                .HasColumnName("health_inactive_threshold");

            builder.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("GETUTCDATE()");

            builder.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("GETUTCDATE()");

            builder.HasIndex(x => x.UserId).IsUnique();

            builder.HasOne(x => x.User)
                .WithOne(x => x.UserSetting)
                .HasForeignKey<UserSetting>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
