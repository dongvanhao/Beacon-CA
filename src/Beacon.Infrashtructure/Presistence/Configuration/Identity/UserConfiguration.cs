using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Setting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrastructure.Persistence.Configurations.Identity;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(x => x.Email)
            .IsUnique();

        builder.Property(x => x.PasswordHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(x => x.TimeZone)
            .IsRequired()
            .HasMaxLength(100);


        builder.HasOne(x => x.AvatarMediaObject)
            .WithMany()
            .HasForeignKey(x => x.AvatarMediaObjectId)
            .OnDelete(DeleteBehavior.SetNull);

        // === CÁC BẢNG TÍNH NĂNG CHƯA LÀM TỚI (Tạm ẩn) ===
        // builder.HasOne(x => x.SafetySetting)
        //     .WithOne(x => x.User)
        //     .HasForeignKey<SafetySetting>(x => x.UserId)
        //     .OnDelete(DeleteBehavior.Cascade);

        // builder.HasOne(x => x.NotificationPreference)
        //     .WithOne(x => x.User)
        //     .HasForeignKey<NotificationPreference>(x => x.UserId)
        //     .OnDelete(DeleteBehavior.Cascade);

        // builder.HasOne(x => x.AppPreference)
        //     .WithOne(x => x.User)
        //     .HasForeignKey<AppPreference>(x => x.UserId)
        //     .OnDelete(DeleteBehavior.Cascade);
    }
}