using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Persistence.Configurations;

public class ProviderWebhookConfiguration : IEntityTypeConfiguration<ProviderWebhook>
{
    public void Configure(EntityTypeBuilder<ProviderWebhook> builder)
    {
        builder.ToTable("provider_webhooks");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(50).IsRequired();
        builder.Property(x => x.ProviderEventId).HasColumnName("provider_event_id").HasMaxLength(255).IsRequired();
        builder.Property(x => x.ProcessingState).HasColumnName("processing_state").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
        builder.Property(x => x.ReceivedAt).HasColumnName("received_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(x => new { x.Provider, x.ProviderEventId }).IsUnique();
        builder.HasIndex(x => new { x.ProcessingState, x.ReceivedAt });
    }
}
