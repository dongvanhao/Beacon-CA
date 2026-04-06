using Beacon.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainUser = Beacon.Domain.Entities.User.User;

namespace Beacon.Infrashtructure.Presistence.Configuration.User
{
    public class UserConfiguration : IEntityTypeConfiguration<DomainUser>
    {
        public void Configure(EntityTypeBuilder<DomainUser> builder)
        {
            builder.ToTable("users", "user");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.UserName)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("user_name");

            builder.Property(x => x.PasswordHash)
                .IsRequired()
                .HasMaxLength(500)
                .HasColumnName("password_hash");

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("name");

            builder.Property(x => x.Email)
                .HasMaxLength(256)
                .HasColumnName("email");

            builder.Property(x => x.Phone)
                .HasMaxLength(30)
                .HasColumnName("phone");

            builder.Property(x => x.AvatarMediaId)
                .HasColumnName("avatar_media_id");

            builder.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("GETUTCDATE()");

            builder.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("GETUTCDATE()");

            builder.HasIndex(x => x.UserName).IsUnique();
            builder.HasIndex(x => x.Email).IsUnique().HasFilter("[email] IS NOT NULL");
            builder.HasIndex(x => x.Phone).IsUnique().HasFilter("[phone] IS NOT NULL");
            builder.HasIndex(x => x.AvatarMediaId);

            builder.HasOne(x => x.AvatarMedia)
                .WithMany(x => x.AvatarUsers)
                .HasForeignKey(x => x.AvatarMediaId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
