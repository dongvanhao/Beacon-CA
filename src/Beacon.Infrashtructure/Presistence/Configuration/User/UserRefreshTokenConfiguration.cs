using Beacon.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrashtructure.Presistence.Configuration.User
{
    public class UserRefreshTokenConfiguration : IEntityTypeConfiguration<UserRefreshToken>
    {
        public void Configure(EntityTypeBuilder<UserRefreshToken> builder)
        {
            builder.ToTable("refresh_tokens", "user");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.UserId)
                .HasColumnName("user_id");

            builder.Property(x => x.TokenHash)
                .IsRequired()
                .HasMaxLength(500)
                .HasColumnName("token_hash");

            builder.Property(x => x.ExpiresAt)
                .IsRequired()
                .HasColumnName("expires_at");

            builder.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("GETUTCDATE()");

            builder.Property(x => x.Revoked)
                .HasDefaultValue(false)
                .HasColumnName("revoked");

            builder.Property(x => x.UserAgent)
                .HasMaxLength(500)
                .HasColumnName("user_agent");

            builder.Property(x => x.IpAddress)
                .HasMaxLength(100)
                .HasColumnName("ip_address");

            builder.HasIndex(x => x.TokenHash).IsUnique();
            builder.HasIndex(x => x.UserId);
            builder.HasIndex(x => new { x.UserId, x.Revoked });
            builder.HasIndex(x => x.ExpiresAt);
            builder.HasIndex(x => new { x.UserId, x.CreatedAt });

            builder.HasOne(x => x.User)
                .WithMany(x => x.RefreshTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
