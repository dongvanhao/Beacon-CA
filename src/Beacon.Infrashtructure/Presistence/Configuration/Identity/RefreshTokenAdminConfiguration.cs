using Beacon.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrastructure.Persistence.Configurations.Identity;

public class RefreshTokenAdminConfiguration : IEntityTypeConfiguration<RefreshTokenAdmin>
{
    public void Configure(EntityTypeBuilder<RefreshTokenAdmin> builder)
    {
        builder.ToTable("RefreshTokenAdmins");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Token)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasIndex(x => x.Token)
            .IsUnique();


        builder.Property(x => x.ReplacedByToken)
            .HasMaxLength(500);

        builder.HasOne(x => x.Admin)
            .WithMany(x => x.RefreshTokens)
            .HasForeignKey(x => x.AdminId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
