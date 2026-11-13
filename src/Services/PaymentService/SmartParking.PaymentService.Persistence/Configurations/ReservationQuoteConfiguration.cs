using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;

namespace SmartParking.PaymentService.Persistence.Configurations;

public class ReservationQuoteConfiguration : IEntityTypeConfiguration<ReservationQuote>
{
    public void Configure(EntityTypeBuilder<ReservationQuote> builder)
    {
        builder.ToTable("reservation_quotes");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.SiteId).HasColumnName("site_id").IsRequired();
        builder.Property(x => x.AccountId).HasColumnName("account_id").IsRequired();
        builder.Property(x => x.VehicleId).HasColumnName("vehicle_id");
        builder.Property(x => x.RequestedSlotId).HasColumnName("requested_slot_id");
        builder.Property(x => x.StartTime).HasColumnName("start_time").IsRequired();
        builder.Property(x => x.EndTime).HasColumnName("end_time").IsRequired();

        builder.Property(x => x.BookedAmount).HasColumnName("booked_amount").HasPrecision(14, 2).IsRequired();
        builder.Property(x => x.DepositAmount).HasColumnName("deposit_amount").HasPrecision(14, 2).IsRequired();
        builder.Property(x => x.PrepaymentMode).HasColumnName("prepayment_mode").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(10).HasDefaultValue("VND").IsRequired();

        builder.Property(x => x.TariffId).HasColumnName("tariff_id");
        builder.Property(x => x.TariffRevisionId).HasColumnName("tariff_revision_id");
        builder.Property(x => x.TermsVersion).HasColumnName("terms_version").HasMaxLength(50).HasDefaultValue("v1").IsRequired();
        builder.Property(x => x.CoverageVersion).HasColumnName("coverage_version").HasMaxLength(100);

        builder.Property(x => x.ValidUntil).HasColumnName("valid_until").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50).HasDefaultValue(ReservationQuoteStatus.ISSUED).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(x => x.AccountId);
        builder.HasIndex(x => new { x.SiteId, x.Status });
        builder.HasIndex(x => x.ValidUntil);
    }
}
