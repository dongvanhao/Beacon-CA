using Beacon.Domain.Entities.Setting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Infrashtructure.Presistence.Configuration.Setting;

public class SafetySettingConfiguration : IEntityTypeConfiguration<SafetySetting>
{
    public void Configure(EntityTypeBuilder<SafetySetting> builder)
    {
        builder.ToTable("SafetySettings");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.UserId)
            .IsUnique();

        builder.Property(x => x.DailyDeadlineLocalTime)
            .IsRequired();

        builder.Property(x => x.GracePeriodMinutes)
            .IsRequired();

        builder.Property(x => x.ReminderBeforeMinutes)
            .IsRequired();

        builder.Property(x => x.AutoAlertDelayMinutes)
            .IsRequired();

        builder.HasOne(x => x.User)
            .WithOne(x => x.SafetySetting)
            .HasForeignKey<SafetySetting>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
