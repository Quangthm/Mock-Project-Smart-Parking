using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Persistence.Configurations;

public class PolicyRevisionConfiguration : IEntityTypeConfiguration<PolicyRevision>
{
    public void Configure(EntityTypeBuilder<PolicyRevision> builder)
    {
        builder.ToTable("policy_revisions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id");

        builder.Property(p => p.PolicyId)
            .HasColumnName("policy_id")
            .IsRequired();

        builder.Property(p => p.PolicyType)
            .HasColumnName("policy_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.RevisionNumber)
            .HasColumnName("revision_number")
            .IsRequired();

        builder.Property(p => p.EffectiveFrom)
            .HasColumnName("effective_from")
            .IsRequired();

        builder.Property(p => p.EffectiveTo)
            .HasColumnName("effective_to");

        builder.Property(p => p.ScopeType)
            .HasColumnName("scope_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.ScopeId)
            .HasColumnName("scope_id");

        builder.Property(p => p.PolicyValuesJson)
            .HasColumnName("policy_values")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(p => p.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(p => new { p.PolicyId, p.RevisionNumber })
            .IsUnique();

        builder.HasIndex(p => new { p.PolicyId, p.EffectiveFrom })
            .HasDatabaseName("idx_policy_revisions_lookup");

        builder.HasIndex(p => new { p.ScopeType, p.ScopeId })
            .HasDatabaseName("idx_policy_revisions_scope");
    }
}
