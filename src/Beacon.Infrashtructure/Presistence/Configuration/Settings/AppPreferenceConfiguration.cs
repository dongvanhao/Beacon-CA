using Beacon.Domain.Entities.Setting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrastructure.Persistence.Configurations.Settings;

public class AppPreferenceConfiguration : IEntityTypeConfiguration<AppPreference>
{
    public void Configure(EntityTypeBuilder<AppPreference> builder)
    {
        builder.ToTable("AppPreferences");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.UserId)
            .IsUnique();

        builder.Property(x => x.LanguageCode)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(x => x.Theme)
            .IsRequired()
            .HasMaxLength(20);
    }
}