using Microsoft.EntityFrameworkCore;
using SmartParking.UserService.Domain.Entities;

namespace UserService.Persistence;

public partial class AppDbContext
{
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<MediaHold> MediaHolds => Set<MediaHold>();

    private static void ConfigureMedia(ModelBuilder builder)
    {
        var asset = builder.Entity<MediaAsset>();
        asset.ToTable("media_assets");
        asset.HasKey(a => a.Id);
        foreach (var property in typeof(MediaAsset).GetProperties())
            asset.Property(property.Name).HasColumnName(Snake(property.Name));
        asset.Property(a => a.Version).IsConcurrencyToken();
        asset.HasIndex(a => new { a.ScopeId, a.Purpose, a.IdempotencyKey }).IsUnique().HasDatabaseName("uq_media_upload_key");
        asset.HasIndex(a => new { a.Provider, a.Environment, a.PublicId, a.ResourceType, a.DeliveryType }).IsUnique().HasDatabaseName("uq_media_provider_locator");
        asset.HasIndex(a => new { a.State, a.RetryAt, a.LeaseUntil }).HasDatabaseName("idx_media_pending");
        asset.HasIndex(a => a.RetainUntil).HasDatabaseName("idx_media_due");
        asset.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
        asset.HasOne<Account>().WithMany().HasForeignKey(a => new { a.CreatorAccountId, a.UserId })
            .HasPrincipalKey(a => new { a.Id, a.UserId }).OnDelete(DeleteBehavior.Restrict);
        asset.HasOne<RegisteredVehicle>().WithMany().HasForeignKey(a => a.VehicleId).OnDelete(DeleteBehavior.Restrict);
        var hold = builder.Entity<MediaHold>();
        hold.ToTable("media_holds");
        hold.HasKey(h => h.Id);
        hold.Ignore(h => h.Active);
        foreach (var property in typeof(MediaHold).GetProperties().Where(p => p.Name != nameof(MediaHold.Active)))
            hold.Property(property.Name).HasColumnName(Snake(property.Name));
        hold.HasOne<MediaAsset>().WithMany().HasForeignKey(h => h.AssetId).OnDelete(DeleteBehavior.Restrict);
        hold.HasOne<Account>().WithMany().HasForeignKey(h => h.ActorAccountId).OnDelete(DeleteBehavior.Restrict);
        hold.HasIndex(h => new { h.AssetId, h.OriginatingHoldId }).IsUnique();
        static string Snake(string name) => System.Text.RegularExpressions.Regex.Replace(name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
    }
}
