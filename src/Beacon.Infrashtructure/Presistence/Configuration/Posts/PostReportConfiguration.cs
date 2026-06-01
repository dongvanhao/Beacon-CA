using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Posts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrashtructure.Presistence.Configuration.Posts;

public class PostReportConfiguration : IEntityTypeConfiguration<PostReport>
{
    public void Configure(EntityTypeBuilder<PostReport> builder)
    {
        builder.ToTable("PostReports");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PostId).IsRequired();
        builder.Property(x => x.ReporterUserId).IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.ResolutionNote)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.HasOne(x => x.Post)
            .WithMany()
            .HasForeignKey(x => x.PostId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReporterUser)
            .WithMany()
            .HasForeignKey(x => x.ReporterUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReviewedByAdmin)
            .WithMany()
            .HasForeignKey(x => x.ReviewedByAdminId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc })
            .HasDatabaseName("IX_PostReports_Status_CreatedAtUtc");

        builder.HasIndex(x => x.PostId)
            .HasDatabaseName("IX_PostReports_PostId");

        builder.HasIndex(x => x.ReporterUserId)
            .HasDatabaseName("IX_PostReports_ReporterUserId");
    }
}
