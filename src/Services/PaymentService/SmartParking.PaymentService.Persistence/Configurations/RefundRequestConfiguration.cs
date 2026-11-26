using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Persistence.Configurations;

public class RefundRequestConfiguration : IEntityTypeConfiguration<RefundRequest>
{
    public void Configure(EntityTypeBuilder<RefundRequest> builder)
    {
        builder.ToTable("refund_requests");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.PaymentOrderId).HasColumnName("payment_order_id").IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(14, 2).IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").IsRequired();
        builder.Property(x => x.EvidenceUrl).HasColumnName("evidence_url").HasMaxLength(500);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
        builder.Property(x => x.ApprovedBy).HasColumnName("approved_by");
        builder.Property(x => x.HandledByOwner).HasColumnName("handled_by_owner");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne(x => x.PaymentOrder)
            .WithMany()
            .HasForeignKey(x => x.PaymentOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PaymentOrderId).HasDatabaseName("idx_refund_requests_order");
        builder.HasIndex(x => x.Status).HasDatabaseName("idx_refund_requests_status");
    }
}
