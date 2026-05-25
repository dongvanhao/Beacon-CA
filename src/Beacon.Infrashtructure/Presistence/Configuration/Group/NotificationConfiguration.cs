using Beacon.Domain.Entities.Group;
using Beacon.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotificationEntity = Beacon.Domain.Entities.Group.Notification;

namespace Beacon.Infrashtructure.Presistence.Configuration.Group;

public class NotificationConfiguration : IEntityTypeConfiguration<NotificationEntity>
{
    public void Configure(EntityTypeBuilder<NotificationEntity> b)
    {
        b.ToTable("Notifications");
        b.HasKey(n => n.Id);

        b.Property(n => n.Type).IsRequired().HasConversion<int>();
        b.Property(n => n.Title).IsRequired().HasMaxLength(256);
        b.Property(n => n.Body).IsRequired().HasMaxLength(1024);
        b.Property(n => n.Data).HasMaxLength(4000);
        b.Property(n => n.IsRead).IsRequired();

        b.HasOne<User>().WithMany()
         .HasForeignKey(n => n.ReceiverUserId)
         .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(n => new { n.ReceiverUserId, n.CreatedAtUtc })
         .HasDatabaseName("IX_Notifications_ReceiverUserId_CreatedAtUtc");

        b.HasIndex(n => new { n.ReceiverUserId, n.IsRead })
         .HasDatabaseName("IX_Notifications_ReceiverUserId_IsRead");
    }
}
