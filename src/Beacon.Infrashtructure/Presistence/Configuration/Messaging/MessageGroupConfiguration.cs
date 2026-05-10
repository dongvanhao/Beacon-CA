using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Enums.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrashtructure.Presistence.Configuration.Messaging;

public class MessageGroupConfiguration : IEntityTypeConfiguration<MessageGroup>
{
    public void Configure(EntityTypeBuilder<MessageGroup> b)
    {
        b.ToTable("MessageGroups");
        b.HasKey(g => g.Id);

        b.Property(g => g.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(MessageGroupType.Direct)
            .IsRequired();

        b.Property(g => g.DirectKey)
            .HasMaxLength(100)
            .IsRequired(false);

        b.HasIndex(g => g.DirectKey)
            .IsUnique()
            .HasFilter("[DirectKey] IS NOT NULL")
            .HasDatabaseName("UX_MessageGroups_DirectKey");

        b.Property(g => g.CreatedAtUtc).IsRequired();
        b.Property(g => g.Name).HasMaxLength(100);
        b.Property(g => g.AvatarMediaObjectId).IsRequired(false);
        b.HasOne(g => g.AvatarMedia).WithMany()
         .HasForeignKey(g => g.AvatarMediaObjectId)
         .OnDelete(DeleteBehavior.SetNull)
         .IsRequired(false);
        b.Property(g => g.IsDeleted).IsRequired().HasDefaultValue(false);
        b.Property(g => g.DeletedAtUtc);
    }
}
