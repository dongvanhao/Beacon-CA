using Beacon.Domain.Entities.Group;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrashtructure.Presistence.Configuration.Group;

public class FriendRequestConfiguration : IEntityTypeConfiguration<FriendRequest>
{
    public void Configure(EntityTypeBuilder<FriendRequest> b)
    {
        b.ToTable("FriendRequests");
        b.HasKey(r => r.Id);
        b.Property(r => r.Status).IsRequired().HasConversion<int>();
        b.Property(r => r.CreatedAtUtc).IsRequired();

        b.HasOne(r => r.Sender).WithMany()
         .HasForeignKey(r => r.SenderId).OnDelete(DeleteBehavior.Restrict);
    }
}
