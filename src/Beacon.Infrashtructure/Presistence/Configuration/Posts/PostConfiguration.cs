using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Entities.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrashtructure.Presistence.Configuration.Posts;

public class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("Posts");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.OwnerUserId).IsRequired();
        builder.Property(p => p.MediaId).IsRequired();

        builder.Property(p => p.Caption)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(p => p.Visibility)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(p => p.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(p => p.DeletedAtUtc).IsRequired(false);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(p => p.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<MediaObject>()
            .WithMany()
            .HasForeignKey(p => p.MediaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.OwnerUserId, p.CreatedAtUtc, p.Id })
            .HasDatabaseName("IX_Posts_OwnerUserId_CreatedAtUtc");

        builder.HasIndex(p => new { p.Status, p.DeletedAtUtc, p.CreatedAtUtc, p.Id })
            .HasDatabaseName("IX_Posts_Feed_Filter");
    }
}
