using Microsoft.EntityFrameworkCore;
using SmartParking.ReservationService.Domain.Entities;

namespace SmartParking.ReservationService.Persistence;

public sealed class ReservationDbContext(DbContextOptions<ReservationDbContext> options) : DbContext(options)
{
    public DbSet<ReservationRow> Reservations => Set<ReservationRow>();
    public DbSet<ParkingSessionRow> ParkingSessions => Set<ParkingSessionRow>();
    public DbSet<SlotAllocationRow> SlotAllocations => Set<SlotAllocationRow>();
    public DbSet<SiteCapacityPoolRow> SiteCapacityPools => Set<SiteCapacityPoolRow>();
    public DbSet<StructureEditHoldRow> StructureEditHolds => Set<StructureEditHoldRow>();
    public DbSet<StructureSiteStateRow> StructureSiteStates => Set<StructureSiteStateRow>();
    public DbSet<StructureRemovedResourceRow> StructureRemovedResources => Set<StructureRemovedResourceRow>();
    public DbSet<StructureReleaseReceiptRow> StructureReleaseReceipts => Set<StructureReleaseReceiptRow>();
    public DbSet<AuditLogRow> AuditLogs => Set<AuditLogRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        Map<ReservationRow>(modelBuilder, "reservations");
        Map<ParkingSessionRow>(modelBuilder, "parking_sessions");
        Map<SlotAllocationRow>(modelBuilder, "slot_allocations");
        Map<SiteCapacityPoolRow>(modelBuilder, "site_capacity_pools");
        Map<StructureEditHoldRow>(modelBuilder, "structure_edit_holds");
        Map<StructureSiteStateRow>(modelBuilder, "structure_site_state");
        Map<StructureRemovedResourceRow>(modelBuilder, "structure_removed_resources");
        Map<StructureReleaseReceiptRow>(modelBuilder, "structure_release_receipts");
        Map<AuditLogRow>(modelBuilder, "audit_logs");
        modelBuilder.Entity<StructureEditHoldRow>().HasKey(x => x.SiteId);
        modelBuilder.Entity<StructureEditHoldRow>().HasIndex(x => x.Token).IsUnique();
        modelBuilder.Entity<StructureSiteStateRow>().HasKey(x => x.SiteId);
        modelBuilder.Entity<StructureReleaseReceiptRow>().HasKey(x => x.Token);
        modelBuilder.Entity<StructureReleaseReceiptRow>().Property(x => x.Outcome).HasColumnType("jsonb");
    }

    private static void Map<TEntity>(ModelBuilder modelBuilder, string table) where TEntity : class
    {
        var entity = modelBuilder.Entity<TEntity>();
        entity.ToTable(table);
        foreach (var property in entity.Metadata.GetProperties())
            property.SetColumnName(ToSnakeCase(property.Name));
    }

    private static string ToSnakeCase(string value)
    {
        var result = new System.Text.StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsUpper(value[i]) && i > 0)
                result.Append('_');
            result.Append(char.ToLowerInvariant(value[i]));
        }
        return result.ToString();
    }
}
