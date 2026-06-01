using Beacon.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrashtructure.Presistence.Configuration.Identity;

public class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
{
    public void Configure(EntityTypeBuilder<AdminAuditLog> builder)
    {
        builder.ToTable("AdminAuditLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AdminUsername).HasMaxLength(100);
        builder.Property(x => x.HttpMethod).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Path).HasMaxLength(500).IsRequired();
        builder.Property(x => x.QueryString).HasMaxLength(1000);
        builder.Property(x => x.Controller).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(150).IsRequired();
        builder.Property(x => x.EntityName).HasMaxLength(100);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(512);

        builder.Property(x => x.RequestJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.OldDataJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.NewDataJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ResponseJson).HasColumnType("nvarchar(max)");

        builder.HasIndex(x => x.AdminId);
        builder.HasIndex(x => new { x.EntityName, x.EntityId });
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
