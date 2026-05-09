using Beacon.Domain.Entities.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrashtructure.Presistence.Configuration.Messaging;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> b)
    {
        b.ToTable("Messages");
        b.HasKey(m => m.Id);
        b.Property(m => m.Content).IsRequired().HasMaxLength(4000);
        b.Property(m => m.CreatedAtUtc).IsRequired();
        b.Property(m => m.IsDeleted).HasDefaultValue(false);
        b.Property(m => m.ClientMessageId).HasMaxLength(100);

        b.Property(m => m.SequenceNumber)
            .ValueGeneratedOnAdd()
            .UseIdentityColumn();

        b.HasIndex(m => new { m.GroupId, m.SequenceNumber });
        b.HasIndex(m => new { m.GroupId, m.CreatedAtUtc });

        // Idempotency: unique per group, skips NULL ClientMessageId
        b.HasIndex(m => new { m.GroupId, m.ClientMessageId })
            .IsUnique()
            .HasFilter("[ClientMessageId] IS NOT NULL");

        b.HasOne(m => m.Group).WithMany(g => g.Messages)
         .HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(m => m.Sender).WithMany()
         .HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne<Message>()
         .WithMany()
         .HasForeignKey(m => m.ReplyToMessageId)
         .OnDelete(DeleteBehavior.ClientSetNull)
         .IsRequired(false);
    }
}
