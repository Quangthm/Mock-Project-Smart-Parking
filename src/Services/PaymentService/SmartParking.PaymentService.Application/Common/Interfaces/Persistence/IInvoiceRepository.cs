using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Application.Common.Interfaces.Persistence;

public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Invoice?> GetByPaymentOrderIdAsync(Guid paymentOrderId, CancellationToken cancellationToken = default);
    Task<Invoice?> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default);
    Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default);
    void Update(Invoice invoice);
    Task<string> GenerateInvoiceNumberAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);
}
