using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Persistence.Configurations;

public class TariffConfiguration : IEntityTypeConfiguration<Tariff>
{
    public void Configure(EntityTypeBuilder<Tariff> builder)
    {
        builder.ToTable("tariffs");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id");

        builder.Property(t => t.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(t => t.SiteId)
            .HasColumnName("site_id")
            .IsRequired();

        builder.Property(t => t.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.VehicleType)
            .HasColumnName("vehicle_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.FreeParkingGraceMinutes)
            .HasColumnName("free_parking_grace_minutes")
            .HasDefaultValue(15);

        builder.Property(t => t.TariffRulesJson)
            .HasColumnName("tariff_rules")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(t => t.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(t => t.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasQueryFilter(t => t.IsActive && t.DeletedAt == null);

        builder.HasIndex(t => new { t.SiteId, t.VehicleType })
            .HasDatabaseName("idx_tariffs_site_vehicle");
    }
}
