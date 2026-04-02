using Beacon.Domain.Entities.Checkins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Infrashtructure.Presistence.Configuration.Checkins
{
    public class CheckinConfiguration : IEntityTypeConfiguration<Checkin>
    {
        public void Configure(EntityTypeBuilder<Checkin> builder)
        {
            builder.ToTable("Checkins");

            builder.HasKey(x => x.Id);

            builder.HasIndex(x => x.DailySafetyRecordId)
                .IsUnique();

            builder.Property(x => x.Type)
                .HasConversion<int>();

            builder.Property(x => x.Note)
                .HasMaxLength(1000);

            builder.Property(x => x.Latitude)
                .HasPrecision(9, 6);

            builder.Property(x => x.Longitude)
                .HasPrecision(9, 6);

            builder.HasOne(x => x.User)
                .WithMany(x => x.Checkins)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.DailySafetyRecord)
                .WithOne(x => x.Checkin)
                .HasForeignKey<Checkin>(x => x.DailySafetyRecordId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
