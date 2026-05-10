using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrashtructure.Presistence.Configuration.Messaging;

public class MessageGroupMemberConfiguration : IEntityTypeConfiguration<MessageGroupMember>
{
    public void Configure(EntityTypeBuilder<MessageGroupMember> b)
    {
        b.ToTable("MessageGroupMembers");
        b.HasKey(m => new { m.GroupId, m.UserId });
        b.HasIndex(m => m.UserId);

        b.Property(m => m.Role).IsRequired().HasConversion<int>();
        b.Property(m => m.JoinedAtUtc).IsRequired();
        b.Property(m => m.InvitedByUserId).IsRequired(false);
        b.Property(m => m.LastSeenMessageId).IsRequired(false);

        b.HasOne(m => m.Group).WithMany(g => g.Members)
         .HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne(m => m.User).WithMany()
         .HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne<User>()
         .WithMany()
         .HasForeignKey(m => m.InvitedByUserId)
         .OnDelete(DeleteBehavior.SetNull)
         .IsRequired(false);
    }
}
