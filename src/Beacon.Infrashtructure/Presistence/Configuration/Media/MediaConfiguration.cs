using Beacon.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrashtructure.Presistence.Configuration.User
{
    public class MediaConfiguration : IEntityTypeConfiguration<Media>
    {
        public void Configure(EntityTypeBuilder<Media> builder)
        {
            builder.ToTable("media", "media");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Bucket)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("bucket");

            builder.Property(x => x.ObjectKey)
                .IsRequired()
                .HasMaxLength(500)
                .HasColumnName("object_key");

            builder.Property(x => x.FileName)
                .HasMaxLength(255)
                .HasColumnName("file_name");

            builder.Property(x => x.Type)
                .HasMaxLength(30)
                .HasColumnName("type");

            builder.Property(x => x.MimeType)
                .HasMaxLength(100)
                .HasColumnName("mime_type");

            builder.Property(x => x.Size)
                .HasColumnName("size");

            builder.Property(x => x.Width)
                .HasColumnName("width");

            builder.Property(x => x.Height)
                .HasColumnName("height");

            builder.Property(x => x.Duration)
                .HasColumnName("duration");

            builder.Property(x => x.CreatedBy)
                .HasColumnName("created_by");

            builder.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("GETUTCDATE()");

            builder.HasIndex(x => x.CreatedBy);
            builder.HasIndex(x => new { x.Bucket, x.ObjectKey }).IsUnique();

            builder.HasOne(x => x.Creator)
                .WithMany(x => x.UploadedMedia)
                .HasForeignKey(x => x.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
