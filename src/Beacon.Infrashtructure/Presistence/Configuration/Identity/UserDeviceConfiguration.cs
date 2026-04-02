using Beacon.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrastructure.Persistence.Configurations.Identity;

public class UserDeviceConfiguration : IEntityTypeConfiguration<UserDevice>
{
    public void Configure(EntityTypeBuilder<UserDevice> builder)
    {
        builder.ToTable("UserDevices");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Platform)
            .HasConversion<int>();

        builder.Property(x => x.DeviceName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.DeviceToken)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.UserId, x.DeviceToken });

        builder.HasOne(x => x.User)
            .WithMany(x => x.Devices)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}