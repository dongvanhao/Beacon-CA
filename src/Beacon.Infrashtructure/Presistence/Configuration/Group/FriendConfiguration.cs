using Beacon.Domain.Entities.Group;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrashtructure.Presistence.Configuration.Group;

public class FriendConfiguration : IEntityTypeConfiguration<Friend>
{
    public void Configure(EntityTypeBuilder<Friend> b)
    {
        b.ToTable("Friends");
        b.HasKey(f => f.Id);
        b.HasIndex(f => new { f.UserId1, f.UserId2 }).IsUnique();
        b.Property(f => f.Type).IsRequired().HasConversion<int>();
        b.Property(f => f.CreatedAtUtc).IsRequired();

        b.HasOne(f => f.User1).WithMany()
         .HasForeignKey(f => f.UserId1).OnDelete(DeleteBehavior.Restrict);

        b.HasOne(f => f.User2).WithMany()
         .HasForeignKey(f => f.UserId2).OnDelete(DeleteBehavior.Restrict);
    }
}
