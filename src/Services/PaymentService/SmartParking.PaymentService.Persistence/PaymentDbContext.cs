using MassTransit;
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
    public DbSet<PaymentOrder> PaymentOrders => Set<PaymentOrder>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<ProviderWebhook> ProviderWebhooks => Set<ProviderWebhook>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<ReservationQuote> ReservationQuotes => Set<ReservationQuote>();
    public DbSet<RefundRequest> RefundRequests => Set<RefundRequest>();
    public DbSet<SubscriptionFinancialTerm> SubscriptionFinancialTerms => Set<SubscriptionFinancialTerm>();
    public DbSet<SubscriptionFinancialTermRevision> SubscriptionFinancialTermRevisions => Set<SubscriptionFinancialTermRevision>();
    public DbSet<SubscriptionClosureCompensation> SubscriptionClosureCompensations => Set<SubscriptionClosureCompensation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.AddTransactionalOutboxEntities();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentDbContext).Assembly);

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
