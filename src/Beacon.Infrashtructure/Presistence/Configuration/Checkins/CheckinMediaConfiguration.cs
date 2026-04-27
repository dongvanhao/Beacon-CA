using Beacon.Domain.Entities.Checkins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrashtructure.Presistence.Configuration.Checkins;

public class CheckinMediaConfiguration : IEntityTypeConfiguration<CheckinMedia>
{
    public void Configure(EntityTypeBuilder<CheckinMedia> builder)
    {
        builder.ToTable("CheckinMedias");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Caption)
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.CheckinId, x.SortOrder });

        builder.HasOne(x => x.Checkin)
            .WithMany(x => x.MediaItems)
            .HasForeignKey(x => x.CheckinId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.MediaObject)
            .WithMany()
            .HasForeignKey(x => x.MediaObjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
