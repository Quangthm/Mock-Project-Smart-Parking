using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Persistence.Entities;

namespace SmartParking.OperationService.Persistence;

public sealed class ReservationDbContext(DbContextOptions<ReservationDbContext> options) : DbContext(options)
{
    public DbSet<ReservationRow> Reservations => Set<ReservationRow>();
    public DbSet<ParkingSessionRow> ParkingSessions => Set<ParkingSessionRow>();
    public DbSet<SlotAllocationRow> SlotAllocations => Set<SlotAllocationRow>();
    public DbSet<SiteCapacityPoolRow> SiteCapacityPools => Set<SiteCapacityPoolRow>();
    public DbSet<StructureEditHoldRow> StructureEditHolds => Set<StructureEditHoldRow>();
    public DbSet<StructureSiteStateRow> StructureSiteStates => Set<StructureSiteStateRow>();
    public DbSet<StructureReleaseReceiptRow> StructureReleaseReceipts => Set<StructureReleaseReceiptRow>();
    public DbSet<OutboxEventRow> OutboxEvents => Set<OutboxEventRow>();
    public DbSet<AuditLogRow> AuditLogs => Set<AuditLogRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        Map<ReservationRow>(modelBuilder, "reservations");
        Map<ParkingSessionRow>(modelBuilder, "parking_sessions");
        Map<SlotAllocationRow>(modelBuilder, "slot_allocations");
        Map<SiteCapacityPoolRow>(modelBuilder, "site_capacity_pools");
        Map<StructureEditHoldRow>(modelBuilder, "structure_edit_holds");
        Map<StructureSiteStateRow>(modelBuilder, "structure_site_state");
        Map<StructureReleaseReceiptRow>(modelBuilder, "structure_release_receipts");
        Map<OutboxEventRow>(modelBuilder, "outbox_events");
        Map<AuditLogRow>(modelBuilder, "audit_logs");
        modelBuilder.Entity<StructureEditHoldRow>().HasKey(x => x.SiteId);
        modelBuilder.Entity<StructureEditHoldRow>().HasIndex(x => x.Token).IsUnique();
        modelBuilder.Entity<StructureSiteStateRow>().HasKey(x => x.SiteId);
        modelBuilder.Entity<StructureReleaseReceiptRow>().HasKey(x => x.Token);
        modelBuilder.Entity<StructureReleaseReceiptRow>().Property(x => x.Outcome).HasColumnType("jsonb");
        modelBuilder.Entity<OutboxEventRow>().HasKey(x => x.EventId);
        modelBuilder.Entity<OutboxEventRow>().Property(x => x.EventType).HasMaxLength(64);
        modelBuilder.Entity<OutboxEventRow>().Property(x => x.Payload).HasColumnType("jsonb");
        modelBuilder.Entity<OutboxEventRow>().HasIndex(x => x.SequenceNumber);
        modelBuilder.Entity<OutboxEventRow>().HasIndex(x => new { x.SiteId, x.SequenceNumber });
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
