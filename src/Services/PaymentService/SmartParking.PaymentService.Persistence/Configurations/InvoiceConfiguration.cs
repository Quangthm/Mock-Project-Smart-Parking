using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.PaymentOrderId).HasColumnName("payment_order_id").IsRequired();
        builder.Property(x => x.SiteId).HasColumnName("site_id").IsRequired();

        builder.Property(x => x.ReferenceType).HasColumnName("reference_type").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.ReferenceId).HasColumnName("reference_id").IsRequired();
        builder.Property(x => x.SessionId).HasColumnName("session_id");

        builder.Property(x => x.InvoiceNumber).HasColumnName("invoice_number").HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.InvoiceNumber).IsUnique();

        builder.Property(x => x.TotalParkedMinutes).HasColumnName("total_parked_minutes").HasDefaultValue(0);
        builder.Property(x => x.BaseParkingFee).HasColumnName("base_parking_fee").HasPrecision(14, 2).HasDefaultValue(0);
        builder.Property(x => x.OverstayPenaltyFee).HasColumnName("overstay_penalty_fee").HasPrecision(14, 2).HasDefaultValue(0);
        builder.Property(x => x.FinalAmount).HasColumnName("final_amount").HasPrecision(14, 2).IsRequired();

        builder.Property(x => x.PricingSnapshot)
            .HasColumnName("pricing_snapshot")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<InvoicePricingSnapshot>(v, (JsonSerializerOptions?)null) ?? new InvoicePricingSnapshot()
            )
            .IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(x => x.PaymentOrder)
            .WithOne(x => x.Invoice)
            .HasForeignKey<Invoice>(x => x.PaymentOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SiteId).HasDatabaseName("idx_invoices_site");
        builder.HasIndex(x => x.PaymentOrderId).HasDatabaseName("idx_invoices_order");
        builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId }).HasDatabaseName("idx_invoices_reference");
        builder.HasIndex(x => x.SessionId).HasDatabaseName("idx_invoices_session");
    }
}
