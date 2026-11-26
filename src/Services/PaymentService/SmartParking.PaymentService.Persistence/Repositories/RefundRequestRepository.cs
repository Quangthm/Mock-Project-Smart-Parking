using Microsoft.EntityFrameworkCore;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Persistence.Repositories;

public class RefundRequestRepository : IRefundRequestRepository
{
    private readonly PaymentDbContext _context;

    public RefundRequestRepository(PaymentDbContext context)
    {
        _context = context;
    }

    public async Task<RefundRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.RefundRequests
            .Include(x => x.PaymentOrder)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<RefundRequest>> GetByPaymentOrderIdAsync(Guid paymentOrderId, CancellationToken cancellationToken = default)
    {
        return await _context.RefundRequests
            .Where(x => x.PaymentOrderId == paymentOrderId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(RefundRequest refundRequest, CancellationToken cancellationToken = default)
    {
        await _context.RefundRequests.AddAsync(refundRequest, cancellationToken);
    }

    public void Update(RefundRequest refundRequest)
    {
        _context.RefundRequests.Update(refundRequest);
    }
}
