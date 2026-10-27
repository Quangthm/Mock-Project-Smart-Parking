using Microsoft.EntityFrameworkCore;
using SmartParking.ParkingService.Persistence.Entities;

namespace SmartParking.ParkingService.Persistence;

public sealed class ParkingDbContext(DbContextOptions<ParkingDbContext> options) : DbContext(options)
{
    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();
    public DbSet<ParkingSiteEntity> ParkingSites => Set<ParkingSiteEntity>();
    public DbSet<SpatialUnitEntity> SpatialUnits => Set<SpatialUnitEntity>();
    public DbSet<ParkingSlotEntity> ParkingSlots => Set<ParkingSlotEntity>();
    public DbSet<ParkingAccessPathEntity> ParkingAccessPaths => Set<ParkingAccessPathEntity>();
    public DbSet<StructureBackupPolicyEntity> StructureBackupPolicies => Set<StructureBackupPolicyEntity>();
    public DbSet<StructureOperationEntity> StructureOperations => Set<StructureOperationEntity>();
    public DbSet<StructureSynchronizationEntity> StructureSynchronizations => Set<StructureSynchronizationEntity>();
    public DbSet<StructureChangeAuditEntity> StructureChangeAudits => Set<StructureChangeAuditEntity>();
    public DbSet<StructureSlotAuditEntity> StructureSlotAudits => Set<StructureSlotAuditEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        Map<TenantEntity>(modelBuilder, "tenants");
        Map<ParkingSiteEntity>(modelBuilder, "parking_sites");
        Map<SpatialUnitEntity>(modelBuilder, "spatial_units");
        Map<ParkingSlotEntity>(modelBuilder, "parking_slots");
        Map<ParkingAccessPathEntity>(modelBuilder, "parking_access_paths");
        Map<StructureBackupPolicyEntity>(modelBuilder, "structure_backup_policies");
        Map<StructureOperationEntity>(modelBuilder, "structure_operations");
        Map<StructureSynchronizationEntity>(modelBuilder, "structure_synchronizations");
        Map<StructureChangeAuditEntity>(modelBuilder, "structure_change_audit");
        Map<StructureSlotAuditEntity>(modelBuilder, "structure_slot_audit");

        modelBuilder.Entity<TenantEntity>().HasKey(x => x.Id);
        modelBuilder.Entity<ParkingSiteEntity>().HasKey(x => x.Id);
        modelBuilder.Entity<ParkingSiteEntity>().HasIndex(x => new { x.TenantId, x.SiteCode }).IsUnique();
        modelBuilder.Entity<SpatialUnitEntity>().HasKey(x => x.Id);
        modelBuilder.Entity<ParkingSlotEntity>().HasKey(x => x.Id);
        modelBuilder.Entity<ParkingAccessPathEntity>().HasKey(x => x.Id);
        modelBuilder.Entity<StructureBackupPolicyEntity>().HasKey(x => x.Id);
        modelBuilder.Entity<StructureOperationEntity>().HasKey(x => x.Id);
        modelBuilder.Entity<StructureSynchronizationEntity>().HasKey(x => x.Token);
        modelBuilder.Entity<StructureChangeAuditEntity>().HasKey(x => x.Id);
        modelBuilder.Entity<StructureSlotAuditEntity>().HasKey(x => x.Id);

        modelBuilder.Entity<StructureSynchronizationEntity>().Property(x => x.Outcome).HasColumnType("jsonb");
        modelBuilder.Entity<StructureChangeAuditEntity>().Property(x => x.BeforeState).HasColumnType("jsonb");
        modelBuilder.Entity<StructureChangeAuditEntity>().Property(x => x.AfterState).HasColumnType("jsonb");
        modelBuilder.Entity<ParkingSlotEntity>().Property(x => x.Coordinates3D).HasColumnType("jsonb");
        modelBuilder.Entity<ParkingSlotEntity>().Property(x => x.Features).HasColumnType("jsonb");
        modelBuilder.Entity<ParkingAccessPathEntity>().Property(x => x.MapData).HasColumnType("jsonb");
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
            if (char.IsUpper(value[i]) && i > 0) result.Append('_');
            result.Append(char.ToLowerInvariant(value[i]));
        }
        return result.ToString();
    }
}
