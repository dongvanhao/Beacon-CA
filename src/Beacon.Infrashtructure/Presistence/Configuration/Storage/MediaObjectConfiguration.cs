using Beacon.Domain.Entities.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrastructure.Persistence.Configurations.Storage;

public class MediaObjectConfiguration : IEntityTypeConfiguration<MediaObject>
{
    public void Configure(EntityTypeBuilder<MediaObject> builder)
    {
        builder.ToTable("MediaObjects");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StorageProvider)
            .HasConversion<int>();

        builder.Property(x => x.AccessType)
            .HasConversion<int>();

        builder.Property(x => x.MediaType)
            .HasConversion<int>();

        builder.Property(x => x.BucketName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.ObjectKey)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.ThumbnailObjectKey)
            .HasMaxLength(1000);

        builder.Property(x => x.OriginalFileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.ETag)
            .HasMaxLength(255);

        builder.Property(x => x.ChecksumSha256)
            .HasMaxLength(128);

        builder.HasIndex(x => new { x.BucketName, x.ObjectKey })
            .IsUnique();

        builder.HasIndex(x => x.UploadProviderByUserId);
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
