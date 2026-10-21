using Microsoft.EntityFrameworkCore;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Persistence.Configurations;

namespace SmartParking.PaymentService.Persistence.Context;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
    {
    }

    public DbSet<PaymentOrder> PaymentOrders => Set<PaymentOrder>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<ProviderWebhook> ProviderWebhooks => Set<ProviderWebhook>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new PaymentOrderConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentTransactionConfiguration());
        modelBuilder.ApplyConfiguration(new ProviderWebhookConfiguration());
    }
}
