using Microsoft.EntityFrameworkCore;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;

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

    public async Task<(IReadOnlyList<Invoice> Items, int TotalCount)> GetByAccountIdPagedAsync(
        Guid accountId,
        string? status,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Invoices
            .AsNoTracking()
            .Include(i => i.PaymentOrder)
            .Where(i => i.PaymentOrder != null && i.PaymentOrder.AccountId == accountId);

        // Filter by PaymentOrderStatus safely with Enum.TryParse
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<PaymentOrderStatus>(status.Trim(), ignoreCase: true, out var orderStatus))
            {
                query = query.Where(i => i.PaymentOrder!.Status == orderStatus);
            }
            else
            {
                // Invalid status parameter returns 0 results cleanly
                query = query.Where(i => false);
            }
        }

        // Filter by CreatedAt range
        if (fromDate.HasValue)
        {
            query = query.Where(i => i.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(i => i.CreatedAt <= toDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var effectivePageNumber = pageNumber < 1 ? 1 : pageNumber;
        var effectivePageSize = pageSize < 1 ? 10 : pageSize;

        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((effectivePageNumber - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
