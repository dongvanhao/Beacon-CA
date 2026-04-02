using Beacon.Domain.Entities.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrastructure.Persistence.Configurations.Notifications;

public class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("NotificationDeliveries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Kind)
            .HasConversion<int>();

        builder.Property(x => x.Channel)
            .HasConversion<int>();

        builder.Property(x => x.Status)
            .HasConversion<int>();

        builder.Property(x => x.Recipient)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.Body)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.FailureReason)
            .HasMaxLength(1000);

        builder.Property(x => x.ProviderMessageId)
            .HasMaxLength(255);

        builder.HasIndex(x => new { x.AlertIncidentId, x.Status });
        builder.HasIndex(x => new { x.UserId, x.SentAtUtc });

        builder.HasOne(x => x.User)
            .WithMany(x => x.NotificationDeliveries)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AlertIncident)
            .WithMany(x => x.NotificationDeliveries)
            .HasForeignKey(x => x.AlertIncidentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.EmergencyContact)
            .WithMany(x => x.NotificationDeliveries)
            .HasForeignKey(x => x.EmergencyContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UserDevice)
            .WithMany(x => x.NotificationDeliveries)
            .HasForeignKey(x => x.UserDeviceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}