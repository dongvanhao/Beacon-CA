
using Beacon.Domain.Entities.Safety;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrastructure.Persistence.Configurations.Alerts;

public class AlertIncidentConfiguration : IEntityTypeConfiguration<AlertIncident>
{
    public void Configure(EntityTypeBuilder<AlertIncident> builder)
    {
        builder.ToTable("AlertIncidents");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.DailySafetyRecordId)
            .IsUnique();

        builder.Property(x => x.Type)
            .HasConversion<int>();

        builder.Property(x => x.Status)
            .HasConversion<int>();

        builder.Property(x => x.Message)
            .HasMaxLength(1000);

        builder.Property(x => x.FailureReason)
            .HasMaxLength(1000);

        builder.HasOne(x => x.User)
            .WithMany(x => x.AlertIncidents)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DailySafetyRecord)
            .WithOne(x => x.AlertIncident)
            .HasForeignKey<AlertIncident>(x => x.DailySafetyRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}