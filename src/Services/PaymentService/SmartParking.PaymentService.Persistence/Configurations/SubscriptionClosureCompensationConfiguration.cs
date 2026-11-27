using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Persistence.Configurations;

public sealed class SubscriptionClosureCompensationConfiguration : IEntityTypeConfiguration<SubscriptionClosureCompensation>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<SubscriptionClosureCompensation> builder)
    {
        builder.ToTable("subscription_closure_compensations");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.SiteId).HasColumnName("site_id").IsRequired();
        builder.Property(x => x.ClosureEventId).HasColumnName("closure_event_id").IsRequired();
        builder.Property(x => x.ClosureType).HasColumnName("closure_type").HasMaxLength(20).IsRequired();
        builder.Property(x => x.PassId).HasColumnName("pass_id").IsRequired();
        builder.Property(x => x.PaymentOrderId).HasColumnName("payment_order_id").IsRequired();
        builder.Property(x => x.RefundRequestId).HasColumnName("refund_request_id");
        builder.Property(x => x.CompensatedFrom).HasColumnName("compensated_from").IsRequired(false);
        builder.Property(x => x.CompensatedTo).HasColumnName("compensated_to").IsRequired(false);
        builder.Property(x => x.RefundAmount).HasColumnName("refund_amount").HasPrecision(14, 2).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(128).IsRequired();
        builder.Property(x => x.IdempotencyPayloadHash).HasColumnName("idempotency_payload_hash").HasMaxLength(128).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

        var intervalsConverter = new ValueConverter<IReadOnlyList<ClosureInterval>, string>(
            v => JsonSerializer.Serialize(v, JsonOptions),
            v => (IReadOnlyList<ClosureInterval>?)JsonSerializer.Deserialize<List<ClosureInterval>>(v, JsonOptions) ?? Array.Empty<ClosureInterval>());

        var intervalsComparer = new ValueComparer<IReadOnlyList<ClosureInterval>>(
            (a, b) => JsonSerializer.Serialize(a, JsonOptions) == JsonSerializer.Serialize(b, JsonOptions),
            v => JsonSerializer.Serialize(v, JsonOptions).GetHashCode(),
            v => v.ToArray());

        builder.Property(x => x.CompensatedIntervals)
            .HasColumnName("compensated_intervals")
            .HasColumnType("jsonb")
            .HasConversion(intervalsConverter, intervalsComparer)
            .IsRequired();

        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => new { x.PaymentOrderId, x.CreatedAt })
            .HasDatabaseName("idx_sub_closure_comp_order");
        builder.HasIndex(x => x.PassId)
            .HasDatabaseName("idx_sub_closure_comp_pass");
    }
}
