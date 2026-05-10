using Beacon.Domain.Entities.Group;
using Beacon.Domain.Entities.Identity;
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

        b.HasOne<User>().WithMany()
         .HasForeignKey(r => r.UserId1).OnDelete(DeleteBehavior.Restrict);

        b.HasOne<User>().WithMany()
         .HasForeignKey(r => r.UserId2).OnDelete(DeleteBehavior.Restrict);

        b.HasOne(r => r.Initiator).WithMany()
         .HasForeignKey(r => r.InitiatorId).OnDelete(DeleteBehavior.Restrict);

        b.Property(r => r.RowVersion).IsRowVersion();

        // Unique: chỉ 1 pending request giữa cùng 1 cặp user
        b.HasIndex(r => new { r.UserId1, r.UserId2 })
         .HasFilter("[Status] = 0")
         .IsUnique()
         .HasDatabaseName("UX_FriendRequests_Pair_Pending");

        b.HasIndex(r => new { r.InitiatorId, r.Status, r.CreatedAtUtc })
         .HasDatabaseName("IX_FriendRequests_Initiator_Status_CreatedAt");

        b.HasIndex(r => new { r.UserId1, r.UserId2, r.Status })
         .HasDatabaseName("IX_FriendRequests_Peers_Status");
    }
}
