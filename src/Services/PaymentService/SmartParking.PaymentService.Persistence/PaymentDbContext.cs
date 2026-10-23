using Microsoft.EntityFrameworkCore;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Persistence;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tariff> Tariffs => Set<Tariff>();
    public DbSet<PolicyRevision> PolicyRevisions => Set<PolicyRevision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentDbContext).Assembly);
    }
}
