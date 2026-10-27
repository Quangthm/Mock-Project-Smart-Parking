using Microsoft.EntityFrameworkCore;

namespace SmartParking.ParkingService.Persistence;

public sealed class ParkingDbContext(DbContextOptions<ParkingDbContext> options) : DbContext(options)
{
    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();
    public DbSet<ParkingSiteEntity> ParkingSites => Set<ParkingSiteEntity>();
    public DbSet<SupportTicketEntity> SupportTickets => Set<SupportTicketEntity>();
    public DbSet<OperatorIncidentEntity> OperatorIncidents => Set<OperatorIncidentEntity>();
    public DbSet<OperatorNotificationEntity> OperatorNotifications => Set<OperatorNotificationEntity>();
    public DbSet<OperatorAuditLogEntity> OperatorAuditLogs => Set<OperatorAuditLogEntity>();
    public DbSet<StructureOperationEntity> StructureOperations => Set<StructureOperationEntity>();
    public DbSet<StructureSynchronizationEntity> StructureSynchronizations => Set<StructureSynchronizationEntity>();
    public DbSet<ParkingSiteEntity> ParkingSitesFull => Set<ParkingSiteEntity>();
    public DbSet<SpatialUnitEntity> SpatialUnits => Set<SpatialUnitEntity>();
    public DbSet<ParkingSlotFullEntity> ParkingSlotsFull => Set<ParkingSlotFullEntity>();
    public DbSet<ParkingAccessPathEntity> ParkingAccessPaths => Set<ParkingAccessPathEntity>();
    public DbSet<StructureBackupPolicyEntity> StructureBackupPolicies => Set<StructureBackupPolicyEntity>();
    public DbSet<StructureChangeAuditEntity> StructureChangeAudits => Set<StructureChangeAuditEntity>();
    public DbSet<StructureSlotAuditEntity> StructureSlotAudits => Set<StructureSlotAuditEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantEntity>(e =>
        {
            e.ToTable("tenants"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Code).HasColumnName("code").HasMaxLength(80);
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(255);
            e.Property(x => x.Email).HasColumnName("contact_email").HasMaxLength(255);
            e.Property(x => x.Phone).HasColumnName("contact_phone").HasMaxLength(20);
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            e.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        });
        modelBuilder.Entity<ParkingSiteEntity>(e =>
        {
            e.ToTable("parking_sites"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            e.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        });
        modelBuilder.Entity<SupportTicketEntity>(e =>
        {
            e.ToTable("support_tickets"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.SiteId).HasColumnName("site_id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(255);
            e.Property(x => x.Message).HasColumnName("message");
            e.Property(x => x.BookingId).HasColumnName("booking_id");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").ValueGeneratedOnAdd().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => new { x.SiteId, x.CreatedAt });
        });
        modelBuilder.Entity<OperatorIncidentEntity>(e =>
        {
            e.ToTable("operator_incident_events"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.SiteId).HasColumnName("site_id");
            e.Property(x => x.ActorId).HasColumnName("actor_id");
            e.Property(x => x.Type).HasColumnName("incident_type").HasMaxLength(80);
            e.Property(x => x.Details).HasColumnName("details");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => new { x.SiteId, x.CreatedAt });
        });
        modelBuilder.Entity<OperatorNotificationEntity>(e =>
        {
            e.ToTable("operator_notifications"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.SiteId).HasColumnName("site_id");
            e.Property(x => x.RecipientId).HasColumnName("recipient_id");
            e.Property(x => x.Type).HasColumnName("notification_type").HasMaxLength(80);
            e.Property(x => x.Title).HasColumnName("title").HasMaxLength(255);
            e.Property(x => x.Message).HasColumnName("message");
            e.Property(x => x.RelatedEntityId).HasColumnName("related_entity_id");
            e.Property(x => x.IsRead).HasColumnName("is_read");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => new { x.RecipientId, x.CreatedAt });
        });
        modelBuilder.Entity<OperatorAuditLogEntity>(e =>
        {
            e.ToTable("operator_audit_logs"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.SiteId).HasColumnName("site_id");
            e.Property(x => x.ActorId).HasColumnName("actor_id");
            e.Property(x => x.ActorRole).HasColumnName("actor_role").HasMaxLength(50);
            e.Property(x => x.Action).HasColumnName("action").HasMaxLength(100);
            e.Property(x => x.TargetEntity).HasColumnName("target_entity").HasMaxLength(100);
            e.Property(x => x.TargetId).HasColumnName("target_id");
            e.Property(x => x.Reason).HasColumnName("reason");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => new { x.SiteId, x.CreatedAt });
        });
        modelBuilder.Entity<StructureOperationEntity>(e =>
        {
            e.ToTable("structure_operations"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.SiteId).HasColumnName("site_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id"); e.Property(x => x.ActorId).HasColumnName("actor_id");
            e.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key"); e.Property(x => x.Request).HasColumnName("request").HasColumnType("jsonb");
            e.Property(x => x.Status).HasColumnName("status"); e.Property(x => x.Reason).HasColumnName("reason");
            e.Property(x => x.Impact).HasColumnName("impact").HasColumnType("jsonb");
            e.Property(x => x.PhysicalCommitted).HasColumnName("physical_committed");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").ValueGeneratedOnAdd().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => new { x.SiteId, x.IdempotencyKey }).IsUnique();
        });
        modelBuilder.Entity<StructureSynchronizationEntity>(e =>
        {
            e.ToTable("structure_synchronizations"); e.HasKey(x => x.Token);
            e.Property(x => x.Token).HasColumnName("token"); e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.SiteId).HasColumnName("site_id"); e.Property(x => x.OperationId).HasColumnName("operation_id");
            e.Property(x => x.State).HasColumnName("state"); e.Property(x => x.Outcome).HasColumnName("outcome").HasColumnType("jsonb");
            e.Property(x => x.CreatedAt).HasColumnName("created_at"); e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });
        modelBuilder.Entity<ParkingSiteEntity>(e =>
        {
            e.Property(x => x.Code).HasColumnName("site_code"); e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Address).HasColumnName("address"); e.Property(x => x.Latitude).HasColumnName("latitude");
            e.Property(x => x.Longitude).HasColumnName("longitude"); e.Property(x => x.TotalPhysicalCapacity).HasColumnName("total_physical_capacity");
        });
        modelBuilder.Entity<SpatialUnitEntity>(e =>
        {
            e.ToTable("spatial_units"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.SiteId).HasColumnName("site_id"); e.Property(x => x.ParentId).HasColumnName("parent_id");
            e.Property(x => x.Path).HasColumnName("path"); e.Property(x => x.UnitType).HasColumnName("unit_type");
            e.Property(x => x.Name).HasColumnName("name"); e.Property(x => x.MaxCapacity).HasColumnName("max_capacity");
            e.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        });
        modelBuilder.Entity<ParkingSlotFullEntity>(e =>
        {
            e.ToTable("parking_slots"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.SiteId).HasColumnName("site_id"); e.Property(x => x.SpatialUnitId).HasColumnName("spatial_unit_id");
            e.Property(x => x.Code).HasColumnName("slot_code"); e.Property(x => x.VehicleType).HasColumnName("supported_vehicle_type");
            e.Property(x => x.SlotType).HasColumnName("slot_type"); e.Property(x => x.PhysicalState).HasColumnName("physical_state");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").ValueGeneratedOnAdd().HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.Coordinates3D).HasColumnName("coordinates_3d").HasColumnType("jsonb");
            e.Property(x => x.Features).HasColumnName("features").HasColumnType("jsonb");
            e.Property(x => x.ReservationState).HasColumnName("reservation_state"); e.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        });
        modelBuilder.Entity<ParkingAccessPathEntity>(e =>
        {
            e.ToTable("parking_access_paths"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.SiteId).HasColumnName("site_id"); e.Property(x => x.Code).HasColumnName("path_code");
            e.Property(x => x.FromUnitId).HasColumnName("from_unit_id"); e.Property(x => x.ToUnitId).HasColumnName("to_unit_id");
            e.Property(x => x.MapData).HasColumnName("map_data").HasColumnType("jsonb"); e.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        });
        modelBuilder.Entity<StructureBackupPolicyEntity>(e =>
        {
            e.ToTable("structure_backup_policies"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.SiteId).HasColumnName("site_id");
            e.Property(x => x.UnitId).HasColumnName("unit_id"); e.Property(x => x.VehicleType).HasColumnName("vehicle_type");
            e.Property(x => x.Count).HasColumnName("count");
        });
        modelBuilder.Entity<StructureChangeAuditEntity>(e =>
        {
            e.ToTable("structure_change_audit"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.SiteId).HasColumnName("site_id");
            e.Property(x => x.ActorId).HasColumnName("actor_id"); e.Property(x => x.OperationId).HasColumnName("operation_id");
            e.Property(x => x.BeforeState).HasColumnName("before_state").HasColumnType("jsonb");
            e.Property(x => x.AfterState).HasColumnName("after_state").HasColumnType("jsonb"); e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });
        modelBuilder.Entity<StructureSlotAuditEntity>(e =>
        {
            e.ToTable("structure_slot_audit"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.SiteId).HasColumnName("site_id");
            e.Property(x => x.SlotId).HasColumnName("slot_id"); e.Property(x => x.ActorId).HasColumnName("actor_id");
            e.Property(x => x.Action).HasColumnName("action"); e.Property(x => x.Reason).HasColumnName("reason");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });
    }

}

public sealed class TenantEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Status { get; set; } = "ACTIVE";
    public DateTimeOffset? DeletedAt { get; set; }
}

public sealed class ParkingSiteEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public DateTimeOffset? DeletedAt { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int TotalPhysicalCapacity { get; set; }
}

public sealed class StructureOperationEntity
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public Guid IdempotencyKey { get; set; }
    public string Request { get; set; } = "";
    public string Status { get; set; } = "pending";
    public string? Reason { get; set; }
    public string? Impact { get; set; }
    public bool PhysicalCommitted { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class StructureSynchronizationEntity
{
    public Guid Token { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid? OperationId { get; set; }
    public string State { get; set; } = "";
    public string? Outcome { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class SpatialUnitEntity { public Guid Id { get; set; } public Guid TenantId { get; set; } public Guid SiteId { get; set; } public Guid? ParentId { get; set; } public string Path { get; set; } = ""; public string UnitType { get; set; } = ""; public string Name { get; set; } = ""; public int MaxCapacity { get; set; } public DateTimeOffset? DeletedAt { get; set; } }
public sealed class ParkingSlotFullEntity { public Guid Id { get; set; } public Guid TenantId { get; set; } public Guid SiteId { get; set; } public Guid SpatialUnitId { get; set; } public string Code { get; set; } = ""; public string VehicleType { get; set; } = ""; public string SlotType { get; set; } = ""; public string PhysicalState { get; set; } = ""; public string? Coordinates3D { get; set; } public string? Features { get; set; } public string? ReservationState { get; set; } public DateTimeOffset UpdatedAt { get; set; } public DateTimeOffset? DeletedAt { get; set; } }
public sealed class ParkingAccessPathEntity { public Guid Id { get; set; } public Guid TenantId { get; set; } public Guid SiteId { get; set; } public string Code { get; set; } = ""; public Guid? FromUnitId { get; set; } public Guid? ToUnitId { get; set; } public string? MapData { get; set; } public DateTimeOffset? DeletedAt { get; set; } }
public sealed class StructureBackupPolicyEntity { public Guid Id { get; set; } public Guid SiteId { get; set; } public Guid? UnitId { get; set; } public string VehicleType { get; set; } = ""; public int Count { get; set; } }
public sealed class StructureChangeAuditEntity { public Guid Id { get; set; } public Guid SiteId { get; set; } public Guid ActorId { get; set; } public Guid? OperationId { get; set; } public string? BeforeState { get; set; } public string? AfterState { get; set; } public DateTimeOffset CreatedAt { get; set; } }
public sealed class StructureSlotAuditEntity { public Guid Id { get; set; } public Guid SiteId { get; set; } public Guid SlotId { get; set; } public Guid ActorId { get; set; } public string Action { get; set; } = ""; public string? Reason { get; set; } public DateTimeOffset CreatedAt { get; set; } }

public sealed class SupportTicketEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid UserId { get; set; }
    public string Subject { get; set; } = "";
    public string Message { get; set; } = "";
    public Guid? BookingId { get; set; }
    public string Status { get; set; } = "open";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class OperatorIncidentEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid ActorId { get; set; }
    public string Type { get; set; } = "";
    public string Details { get; set; } = "";
    public string Status { get; set; } = "reported";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class OperatorNotificationEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid RecipientId { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public Guid? RelatedEntityId { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class OperatorAuditLogEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SiteId { get; set; }
    public Guid? ActorId { get; set; }
    public string ActorRole { get; set; } = "operator";
    public string Action { get; set; } = "";
    public string TargetEntity { get; set; } = "OPERATOR_OPERATION";
    public Guid? TargetId { get; set; }
    public string Reason { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
