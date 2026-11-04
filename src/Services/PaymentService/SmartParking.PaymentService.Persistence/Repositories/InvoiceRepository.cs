using Microsoft.EntityFrameworkCore;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Persistence.Repositories;

public class InvoiceRepository : IInvoiceRepository
{
    private readonly PaymentDbContext _dbContext;

    public InvoiceRepository(PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Invoices
            .Include(i => i.PaymentOrder)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<Invoice?> GetByPaymentOrderIdAsync(Guid paymentOrderId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Invoices
            .Include(i => i.PaymentOrder)
            .FirstOrDefaultAsync(i => i.PaymentOrderId == paymentOrderId, cancellationToken);
    }

    public async Task<Invoice?> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Invoices
            .Include(i => i.PaymentOrder)
            .FirstOrDefaultAsync(i => i.SessionId == sessionId || i.ReferenceId == sessionId, cancellationToken);
    }

    public async Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Invoices
            .Include(i => i.PaymentOrder)
            .FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber, cancellationToken);
    }

    public async Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        await _dbContext.Invoices.AddAsync(invoice, cancellationToken);
    }

    public void Update(Invoice invoice)
    {
        _dbContext.Invoices.Update(invoice);
    }

    public async Task<string> GenerateInvoiceNumberAsync(CancellationToken cancellationToken = default)
    {
        var prefix = $"INV-{DateTimeOffset.UtcNow:yyyyMMdd}-";
        var count = await _dbContext.Invoices.CountAsync(i => i.InvoiceNumber.StartsWith(prefix), cancellationToken);
        var seq = count + 1;

        var candidate = $"{prefix}{seq:D4}";
        while (await _dbContext.Invoices.AnyAsync(i => i.InvoiceNumber == candidate, cancellationToken))
        {
            seq++;
            candidate = $"{prefix}{seq:D4}";
        }

        return candidate;
    }

    public async Task<IReadOnlyList<Invoice>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Invoices
            .Include(i => i.PaymentOrder)
            .Where(i => i.PaymentOrder != null && i.PaymentOrder.AccountId == accountId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
