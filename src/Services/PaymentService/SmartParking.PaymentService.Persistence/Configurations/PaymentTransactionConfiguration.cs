using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Persistence.Configurations;

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("payment_transactions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.PaymentOrderId).HasColumnName("payment_order_id").IsRequired();

        builder.Property(x => x.TransactionType).HasColumnName("transaction_type").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.PaymentMethod).HasColumnName("payment_method").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.GatewayTransactionId).HasColumnName("gateway_transaction_id").HasMaxLength(255);

        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(14, 2).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50).HasDefaultValue(Domain.Enums.PaymentStatus.PENDING);
        builder.Property(x => x.PaidAt).HasColumnName("paid_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(x => x.PaymentOrderId);
        builder.HasIndex(x => new { x.PaymentMethod, x.GatewayTransactionId });
    }
}
