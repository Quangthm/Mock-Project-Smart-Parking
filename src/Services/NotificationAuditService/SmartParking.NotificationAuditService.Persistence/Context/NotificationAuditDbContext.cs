using Microsoft.EntityFrameworkCore;
using SmartParking.NotificationAuditService.Domain.Entities;

namespace SmartParking.NotificationAuditService.Persistence.Context;

public class NotificationAuditDbContext : DbContext
{
    public NotificationAuditDbContext(DbContextOptions<NotificationAuditDbContext> options) : base(options)
    {
    }

    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<DurableNotification> DurableNotifications => Set<DurableNotification>();
    public DbSet<CentralAuditProjection> CentralAuditProjections => Set<CentralAuditProjection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationAuditDbContext).Assembly);
    }
}
