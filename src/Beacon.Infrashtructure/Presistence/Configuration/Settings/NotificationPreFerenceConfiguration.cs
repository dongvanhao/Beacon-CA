#if false
// Chưa dùng — sẽ bật lại khi implement module Settings
using Beacon.Domain.Entities.Setting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Infrashtructure.Presistence.Configuration.Setting
{
    public class NotificationPreFerenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
    {
        public void Configure(EntityTypeBuilder<NotificationPreference> builder)
        {
            builder.ToTable("NotificationPreferences");

            builder.HasKey(x => x.Id);

            builder.HasIndex(x => x.UserId)
                .IsUnique();

            builder.HasOne(x => x.User)
                .WithOne(x => x.NotificationPreference)
                .HasForeignKey<NotificationPreference>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
#endif
