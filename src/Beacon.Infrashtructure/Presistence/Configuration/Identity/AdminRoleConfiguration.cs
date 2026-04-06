using Beacon.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrashtructure.Presistence.Configuration.Identity
{
    public class AdminRoleConfiguration : IEntityTypeConfiguration<AdminRole>
    {
        public void Configure(EntityTypeBuilder<AdminRole> builder)
        {
            builder.ToTable("admin_roles");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.AdminId)
                .HasColumnName("admin_id");

            builder.Property(x => x.RoleId)
                .HasColumnName("role_id");

            builder.HasIndex(x => new { x.AdminId, x.RoleId })
                .IsUnique();

            builder.HasOne(x => x.Admin)
                .WithMany(x => x.AdminRoles)
                .HasForeignKey(x => x.AdminId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Role)
                .WithMany(x => x.AdminRoles)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
