using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Persistence.Configurations;

public class PaymentOrderConfiguration : IEntityTypeConfiguration<PaymentOrder>
{
    public void Configure(EntityTypeBuilder<PaymentOrder> builder)
    {
        builder.ToTable("payment_orders");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.SiteId).HasColumnName("site_id").IsRequired();
        builder.Property(x => x.AccountId).HasColumnName("account_id");

        builder.Property(x => x.OrderCode).HasColumnName("order_code").HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.OrderCode).IsUnique();

        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(255);
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();

        builder.Property(x => x.OrderType).HasColumnName("order_type").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.ReferenceType).HasColumnName("reference_type").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.ReferenceId).HasColumnName("reference_id").IsRequired();

        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(14, 2).IsRequired();
        builder.Property(x => x.DiscountAmount).HasColumnName("discount_amount").HasPrecision(14, 2).HasDefaultValue(0);
        builder.Property(x => x.DepositDeducted).HasColumnName("deposit_deducted").HasPrecision(14, 2).HasDefaultValue(0);
        builder.Property(x => x.FinalAmount).HasColumnName("final_amount").HasPrecision(14, 2).IsRequired();

        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50).HasDefaultValue(Domain.Enums.PaymentOrderStatus.PENDING);
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasMany(x => x.Transactions)
               .WithOne(x => x.PaymentOrder)
               .HasForeignKey(x => x.PaymentOrderId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TenantId, x.SiteId });
        builder.HasIndex(x => x.AccountId);
        builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId });
        builder.HasIndex(x => x.Status);
    }
}
