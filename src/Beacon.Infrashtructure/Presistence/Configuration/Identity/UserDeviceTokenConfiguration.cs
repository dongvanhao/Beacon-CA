using Beacon.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrashtructure.Presistence.Configuration.Identity;

public class UserDeviceTokenConfiguration : IEntityTypeConfiguration<UserDeviceToken>
{
    public void Configure(EntityTypeBuilder<UserDeviceToken> b)
    {
        b.ToTable("UserDeviceTokens");
        b.HasKey(t => t.Id);
        b.Property(t => t.Token).IsRequired().HasMaxLength(1000);
        b.HasIndex(t => t.Token).IsUnique().HasDatabaseName("UX_UserDeviceTokens_Token");
        b.HasIndex(t => new { t.UserId, t.IsActive }).HasDatabaseName("IX_UserDeviceTokens_UserId_IsActive");
        b.Property(t => t.Platform).HasConversion<string>().HasMaxLength(20);
        b.Property(t => t.DeviceId).HasMaxLength(200);
        b.Property(t => t.DeviceName).HasMaxLength(200);
        b.Property(t => t.AppVersion).HasMaxLength(50);
        b.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
