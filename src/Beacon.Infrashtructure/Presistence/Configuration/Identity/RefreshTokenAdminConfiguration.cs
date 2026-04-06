using Beacon.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrashtructure.Presistence.Configuration.Identity
{
    public class RefreshTokenAdminConfiguration : IEntityTypeConfiguration<RefreshTokenAdmin>
    {
        public void Configure(EntityTypeBuilder<RefreshTokenAdmin> builder)
        {
            builder.ToTable("refresh_tokens_admin");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.AdminId)
                .HasColumnName("admin_id");

            builder.Property(x => x.TokenHash)
                .IsRequired()
                .HasMaxLength(500)
                .HasColumnName("token_hash");

            builder.Property(x => x.ExpiresAt)
                .IsRequired()
                .HasColumnName("expires_at");

            builder.Property(x => x.CreatedAt)
                .HasColumnName("created_at");

            builder.Property(x => x.Revoked)
                .HasDefaultValue(false)
                .HasColumnName("revoked");

            builder.Property(x => x.UserAgent)
                .HasMaxLength(500)
                .HasColumnName("user_agent");

            builder.Property(x => x.IpAddress)
                .HasMaxLength(100)
                .HasColumnName("ip_address");

            builder.HasIndex(x => x.AdminId);
            builder.HasIndex(x => x.TokenHash);

            builder.HasOne(x => x.Admin)
                .WithMany(x => x.RefreshTokens)
                .HasForeignKey(x => x.AdminId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
