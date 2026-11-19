using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Persistence.Configurations;

public sealed class SubscriptionFinancialTermConfiguration : IEntityTypeConfiguration<SubscriptionFinancialTerm>
{
    public void Configure(EntityTypeBuilder<SubscriptionFinancialTerm> builder)
    {
        builder.ToTable("subscription_financial_terms");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.SiteId).HasColumnName("site_id").IsRequired();
        builder.Property(x => x.PlanId).HasColumnName("plan_id").IsRequired();
        builder.Property(x => x.ProductType).HasColumnName("product_type").HasMaxLength(20).IsRequired();
        builder.Property(x => x.ActiveRevision).HasColumnName("active_revision").IsRequired();
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(10).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(x => new { x.SiteId, x.PlanId }).IsUnique();
        builder.HasIndex(x => new { x.SiteId, x.IsActive }).HasDatabaseName("idx_sub_fin_terms_site");

        builder.HasMany(x => x.Revisions)
            .WithOne(x => x.FinancialTerm)
            .HasForeignKey(x => x.FinancialTermId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
