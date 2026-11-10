using Microsoft.EntityFrameworkCore;
using SmartParking.UserService.Domain.Entities;

namespace UserService.Persistence;

public partial class AppDbContext
{
    public DbSet<VehicleRegistrationClaim> VehicleRegistrationClaims => Set<VehicleRegistrationClaim>();
    private static void ConfigureVehicleClaims(ModelBuilder builder)
    {
        var claim = builder.Entity<VehicleRegistrationClaim>();
        claim.ToTable("vehicle_registration_claims");
        claim.HasKey(c => c.Id);
        claim.Property(c => c.Id).HasColumnName("id");
        claim.Property(c => c.AccountId).HasColumnName("account_id");
        claim.Property(c => c.RawPlate).HasColumnName("original_plate").HasMaxLength(30);
        claim.Property(c => c.CanonicalPlate).HasColumnName("normalized_plate").HasMaxLength(20);
        claim.Property(c => c.VehicleType).HasColumnName("vehicle_type");
        claim.Property(c => c.ImageAssetId).HasColumnName("image_asset_id");
        claim.Property(c => c.Status).HasColumnName("status");
        claim.Property(c => c.CreatedAt).HasColumnName("created_at");
        claim.HasOne<Account>().WithMany().HasForeignKey(c => c.AccountId).OnDelete(DeleteBehavior.Restrict);
        claim.HasOne<MediaAsset>().WithMany().HasForeignKey(c => c.ImageAssetId).OnDelete(DeleteBehavior.Restrict);
        claim.HasIndex(c => new { c.AccountId, c.CanonicalPlate }).IsUnique().HasDatabaseName("uq_pending_vehicle_claim").HasFilter("status = 'PENDING_DISPUTE'");
        builder.Entity<MediaAsset>().HasOne<VehicleRegistrationClaim>().WithMany().HasForeignKey(a => a.RegistrationClaimId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<RegisteredVehicle>().Property(v => v.BindingRevision).HasColumnName("binding_revision").HasDefaultValue(1L).IsConcurrencyToken();
    }
}
