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
        b.HasIndex(m => new { m.GroupId, m.CreatedAtUtc }); // IX_Messages_GroupId_CreatedAtUtc

        b.HasOne(m => m.Group).WithMany(g => g.Messages)
         .HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(m => m.Sender).WithMany()
         .HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Restrict);
    }
}
