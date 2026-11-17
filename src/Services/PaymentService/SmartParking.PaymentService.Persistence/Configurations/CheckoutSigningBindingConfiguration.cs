using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Persistence.Configurations;

public sealed class CheckoutSigningBindingConfiguration : IEntityTypeConfiguration<CheckoutSigningBinding>
{
    public void Configure(EntityTypeBuilder<CheckoutSigningBinding> builder)
    {
        builder.ToTable("checkout_signing_bindings", table =>
        {
            table.HasCheckConstraint("ck_checkout_binding_positive_code", "order_code > 0");
            table.HasCheckConstraint("ck_checkout_binding_source", "source IN ('CHECKOUT', 'LEGACY_IMPORT', 'LEGACY_POLICY')");
        });
        builder.HasKey(row => row.OrderCode);
        builder.Property(row => row.OrderCode).HasColumnName("order_code").ValueGeneratedNever();
        builder.Property(row => row.PaymentOrderId).HasColumnName("payment_order_id");
        builder.Property(row => row.ProtectedBinding).HasColumnName("protected_binding").IsRequired();
        builder.Property(row => row.Source).HasColumnName("source").HasMaxLength(24).IsRequired();
        builder.Property(row => row.EvidenceReference).HasColumnName("evidence_reference").HasMaxLength(256);
        builder.Property(row => row.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasOne<PaymentOrder>().WithMany().HasForeignKey(row => row.PaymentOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => row.PaymentOrderId).IsUnique();
    }
}
