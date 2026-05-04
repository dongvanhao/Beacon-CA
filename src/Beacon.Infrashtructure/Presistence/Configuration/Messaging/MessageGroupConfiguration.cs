using Beacon.Domain.Entities.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrashtructure.Presistence.Configuration.Messaging;

public class MessageGroupConfiguration : IEntityTypeConfiguration<MessageGroup>
{
    public void Configure(EntityTypeBuilder<MessageGroup> b)
    {
        b.ToTable("MessageGroups");
        b.HasKey(g => g.Id);
        b.Property(g => g.IsPrivate).IsRequired();
        b.Property(g => g.CreatedAtUtc).IsRequired();
    }
}
