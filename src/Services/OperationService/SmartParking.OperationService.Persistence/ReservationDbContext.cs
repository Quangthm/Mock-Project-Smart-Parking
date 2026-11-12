using Microsoft.EntityFrameworkCore;
using MassTransit;
using SmartParking.OperationService.Domain.Reservations;
using SmartParking.OperationService.Domain.Entities;

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
    public DbSet<ReservationRequest> ReservationRequests => Set<ReservationRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddTransactionalOutboxEntities();
        Map<ReservationRequest>(modelBuilder, "reservation_requests");
        Map<ReservationTerms>(modelBuilder, "reservation_terms");
        Map<ReservationFacilityDependency>(modelBuilder, "reservation_facility_dependencies");
        modelBuilder.Entity<ReservationFacilityDependency>().Property(x => x.SnapshotJson).HasColumnType("jsonb");
        modelBuilder.Entity<ReservationFacilityDependency>().Property(x => x.OutcomeJson).HasColumnType("jsonb");
        modelBuilder.Entity<ReservationFacilityDependency>().Property(x => x.Version).IsConcurrencyToken();
        modelBuilder.Entity<ReservationFacilityDependency>().HasIndex(x => new { x.WorkflowId, x.Purpose }).IsUnique();
        modelBuilder.Entity<ReservationTerms>().Property(x => x.SnapshotJson).HasColumnType("jsonb");
        modelBuilder.Entity<ReservationRow>().Property(x => x.AcceptedPricingSnapshot).HasColumnType("jsonb");
        Map<OperationalPolicyRow>(modelBuilder, "operational_policies");
        Map<OperationalCapacityConfig>(modelBuilder, "operational_capacity_configs");
        Map<ReservationCancellationEvidence>(modelBuilder, "reservation_cancellation_evidence");
        modelBuilder.Entity<ReservationCancellationEvidence>().Property(x => x.PolicySnapshot).HasColumnType("jsonb");
        modelBuilder.Entity<ReservationCancellationEvidence>().HasIndex(x => x.ReservationId).IsUnique();
        modelBuilder.Entity<OperationalPolicyRow>().Property(x => x.OverrideBounds).HasColumnType("jsonb");
        modelBuilder.Entity<ReservationRow>().Property(x => x.AcceptedOperationalPolicySnapshot).HasColumnType("jsonb");
        modelBuilder.Entity<ReservationRequest>().Property(x => x.IntentJson).HasColumnType("jsonb");
        modelBuilder.Entity<ReservationRequest>().HasIndex(x => new { x.AccountId, x.Operation, x.IdempotencyKey }).IsUnique();
        Map<ReservationRow>(modelBuilder, "reservations");
        modelBuilder.Entity<ReservationRow>().Property(x => x.Version).IsConcurrencyToken();
        Map<ParkingSessionRow>(modelBuilder, "parking_sessions");
        modelBuilder.Entity<ParkingSessionRow>().Property(x => x.Version).IsConcurrencyToken();
        Map<SlotAllocationRow>(modelBuilder, "slot_allocations");
        Map<SiteCapacityPoolRow>(modelBuilder, "site_capacity_pools");
        Map<StructureEditHoldRow>(modelBuilder, "structure_edit_holds");
        Map<StructureSiteStateRow>(modelBuilder, "structure_site_state");
        Map<StructureReleaseReceiptRow>(modelBuilder, "structure_release_receipts");
        modelBuilder.Entity<StructureEditHoldRow>().HasKey(x => x.SiteId);        modelBuilder.Entity<StructureEditHoldRow>().HasIndex(x => x.Token).IsUnique();
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
