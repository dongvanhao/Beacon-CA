using Beacon.Domain.Entities.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrashtructure.Presistence.Configuration.Messaging;

public class MessageGroupMemberSettingConfiguration : IEntityTypeConfiguration<MessageGroupMemberSetting>
{
    public void Configure(EntityTypeBuilder<MessageGroupMemberSetting> b)
    {
        b.ToTable("MessageGroupMemberSettings");
        b.HasKey(x => new { x.GroupId, x.UserId });

        b.Property(x => x.CustomName).HasMaxLength(100);

        b.HasOne<MessageGroup>()
            .WithMany()
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne<Message>()
            .WithMany()
            .HasForeignKey(x => x.LastReadMessageId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .IsRequired(false);
    }
}
