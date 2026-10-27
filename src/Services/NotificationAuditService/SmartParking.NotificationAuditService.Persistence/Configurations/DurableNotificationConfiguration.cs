using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.NotificationAuditService.Domain.Entities;
using SmartParking.NotificationAuditService.Domain.Enums;

namespace SmartParking.NotificationAuditService.Persistence.Configurations;

public class DurableNotificationConfiguration : IEntityTypeConfiguration<DurableNotification>
{
    public void Configure(EntityTypeBuilder<DurableNotification> builder)
    {
        builder.ToTable("durable_notifications");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id)
            .HasColumnName("id")
            .HasColumnType("uuid");

        builder.Property(n => n.OriginatingEvent)
            .HasColumnName("originating_event")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(n => n.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(n => n.RecipientId)
            .HasColumnName("recipient_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(n => n.Channel)
            .HasColumnName("channel")
            .HasMaxLength(50)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<NotificationChannel>(v))
            .IsRequired();

        builder.Property(n => n.TemplateCode)
            .HasColumnName("template_code")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(n => n.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(n => n.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<NotificationStatus>(v))
            .HasDefaultValue(NotificationStatus.PENDING)
            .IsRequired();

        builder.Property(n => n.Attempts)
            .HasColumnName("attempts")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(n => n.MaxAttempts)
            .HasColumnName("max_attempts")
            .HasDefaultValue(3)
            .IsRequired();

        builder.Property(n => n.LastError)
            .HasColumnName("last_error");

        builder.Property(n => n.ScheduledAt)
            .HasColumnName("scheduled_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.Property(n => n.DeliveredAt)
            .HasColumnName("delivered_at");

        builder.Property(n => n.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.Property(n => n.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.HasIndex(n => n.IdempotencyKey)
            .IsUnique();

        builder.HasIndex(n => new { n.RecipientId, n.Status, n.CreatedAt });

        builder.HasIndex(n => new { n.Status, n.ScheduledAt });
    }
}
