using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Persistence.Configurations;

public sealed class SubscriptionFinancialTermRevisionConfiguration : IEntityTypeConfiguration<SubscriptionFinancialTermRevision>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<SubscriptionFinancialTermRevision> builder)
    {
        builder.ToTable("subscription_financial_term_revisions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.FinancialTermId).HasColumnName("financial_term_id").IsRequired();
        builder.Property(x => x.PlanId).HasColumnName("plan_id").IsRequired();
        builder.Property(x => x.PlanRevision).HasColumnName("plan_revision").IsRequired();
        builder.Property(x => x.RevisionNumber).HasColumnName("revision_number").IsRequired();
        builder.Property(x => x.EffectiveFrom).HasColumnName("effective_from").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(x => x.EffectiveTo).HasColumnName("effective_to");
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(128);
        builder.Property(x => x.IdempotencyPayloadHash).HasColumnName("idempotency_payload_hash").HasMaxLength(128);
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

        var pricesConverter = new ValueConverter<IReadOnlyList<SubscriptionVehiclePrice>, string>(
            v => JsonSerializer.Serialize(v, JsonOptions),
            v => (IReadOnlyList<SubscriptionVehiclePrice>?)JsonSerializer.Deserialize<List<SubscriptionVehiclePrice>>(v, JsonOptions) ?? Array.Empty<SubscriptionVehiclePrice>());

        var pricesComparer = new ValueComparer<IReadOnlyList<SubscriptionVehiclePrice>>(
            (a, b) => JsonSerializer.Serialize(a, JsonOptions) == JsonSerializer.Serialize(b, JsonOptions),
            v => JsonSerializer.Serialize(v, JsonOptions).GetHashCode(),
            v => v.ToArray());

        builder.Property(x => x.VehiclePrices)
            .HasColumnName("vehicle_prices")
            .HasColumnType("jsonb")
            .HasConversion(pricesConverter, pricesComparer)
            .IsRequired();

        var refundConverter = new ValueConverter<SubscriptionRefundPolicy, string>(
            v => JsonSerializer.Serialize(v, JsonOptions),
            v => JsonSerializer.Deserialize<SubscriptionRefundPolicy>(v, JsonOptions) ?? SubscriptionRefundPolicy.M05Default);

        var refundComparer = new ValueComparer<SubscriptionRefundPolicy>(
            (a, b) => JsonSerializer.Serialize(a, JsonOptions) == JsonSerializer.Serialize(b, JsonOptions),
            v => JsonSerializer.Serialize(v, JsonOptions).GetHashCode(),
            v => new SubscriptionRefundPolicy(
                v.VoluntaryBeforeStartPercent,
                v.VoluntaryInPeriodPercent,
                v.FacilityClosurePercent,
                v.RoundingMode));

        builder.Property(x => x.RefundPolicy)
            .HasColumnName("refund_policy")
            .HasColumnType("jsonb")
            .HasConversion(refundConverter, refundComparer)
            .IsRequired();

        builder.HasIndex(x => new { x.FinancialTermId, x.RevisionNumber }).IsUnique();
        builder.HasIndex(x => new { x.PlanId, x.PlanRevision, x.EffectiveFrom })
            .HasDatabaseName("idx_sub_fin_rev_plan");
        builder.HasIndex(x => new { x.FinancialTermId, x.IdempotencyKey })
            .HasDatabaseName("uq_sub_fin_rev_idempotency")
            .IsUnique()
            .HasFilter("idempotency_key IS NOT NULL");
    }
}
