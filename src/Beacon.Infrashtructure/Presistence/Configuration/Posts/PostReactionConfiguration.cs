using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Posts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrashtructure.Presistence.Configuration.Posts;

public class PostReactionConfiguration : IEntityTypeConfiguration<PostReaction>
{
    public void Configure(EntityTypeBuilder<PostReaction> builder)
    {
        builder.ToTable("PostReactions");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.PostId).IsRequired();
        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.Icon).IsRequired().HasMaxLength(10);

        builder.HasIndex(r => new { r.PostId, r.UserId })
            .IsUnique()
            .HasDatabaseName("UX_PostReactions_PostId_UserId");

        builder.HasIndex(r => new { r.PostId, r.Icon })
            .HasDatabaseName("IX_PostReactions_PostId_Icon");

        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("IX_PostReactions_UserId");

        builder.HasOne<Post>()
            .WithMany()
            .HasForeignKey(r => r.PostId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
