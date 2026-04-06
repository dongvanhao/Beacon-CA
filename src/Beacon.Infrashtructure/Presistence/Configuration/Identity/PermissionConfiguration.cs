using Beacon.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrashtructure.Presistence.Configuration.Identity
{
    public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
    {
        public void Configure(EntityTypeBuilder<Permission> builder)
        {
            builder.ToTable("permissions");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(150)
                .HasColumnName("code");

            builder.HasIndex(x => x.Code)
                .IsUnique();

            builder.Property(x => x.Description)
                .HasMaxLength(500)
                .HasColumnName("description");

            builder.Property(x => x.CreatedAt)
                .HasColumnName("created_at");
        }
    }
}
