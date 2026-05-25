using Beacon.Domain.Entities.Safety;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrastructure.Persistence.Configurations.Safety;

public class DailySafetyRecordConfiguration : IEntityTypeConfiguration<DailySafetyRecord>
{
    public void Configure(EntityTypeBuilder<DailySafetyRecord> builder)
    {
        builder.ToTable("DailySafetyRecords");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.UserId, x.Date })
            .IsUnique();

        builder.Property(x => x.Status)
            .HasConversion<int>();

        builder.Property(x => x.DeadlineAtUtc)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasOne(x => x.User)
            .WithMany(x => x.DailySafetyRecords)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
