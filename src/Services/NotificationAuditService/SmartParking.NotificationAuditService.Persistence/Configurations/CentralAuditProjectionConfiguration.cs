using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.NotificationAuditService.Domain.Entities;

namespace SmartParking.NotificationAuditService.Persistence.Configurations;

public class CentralAuditProjectionConfiguration : IEntityTypeConfiguration<CentralAuditProjection>
{
    public void Configure(EntityTypeBuilder<CentralAuditProjection> builder)
    {
        builder.ToTable("central_audit_projections");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasColumnName("id")
            .HasColumnType("uuid");

        builder.Property(a => a.OriginatingService)
            .HasColumnName("originating_service")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.EventId)
            .HasColumnName("event_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(a => a.ActorId)
            .HasColumnName("actor_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(a => a.ActorRole)
            .HasColumnName("actor_role")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.Action)
            .HasColumnName("action")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.ResourceType)
            .HasColumnName("resource_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.ResourceId)
            .HasColumnName("resource_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(a => a.Details)
            .HasColumnName("details")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(a => a.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        builder.Property(a => a.ProjectedAt)
            .HasColumnName("projected_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.HasIndex(a => a.EventId)
            .IsUnique();

        builder.HasIndex(a => new { a.ActorId, a.OccurredAt });

        builder.HasIndex(a => new { a.ResourceType, a.ResourceId });

        builder.HasIndex(a => new { a.OriginatingService, a.OccurredAt });
    }
}
