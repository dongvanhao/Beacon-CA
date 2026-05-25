using Beacon.Domain.Entities.Safety;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrashtructure.Presistence.Configuration.Safety;

public class EmergencyContactConfiguration : IEntityTypeConfiguration<EmergencyContact>
{
    public void Configure(EntityTypeBuilder<EmergencyContact> builder)
    {
        builder.ToTable("EmergencyContacts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.ContactValue)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.Relationship)
            .HasMaxLength(100);

        builder.Property(x => x.ChannelType)
            .HasConversion<int>();

        builder.HasIndex(x => new { x.UserId, x.ContactValue, x.ChannelType });

        builder.HasOne(x => x.User)
            .WithMany(x => x.EmergencyContacts)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
