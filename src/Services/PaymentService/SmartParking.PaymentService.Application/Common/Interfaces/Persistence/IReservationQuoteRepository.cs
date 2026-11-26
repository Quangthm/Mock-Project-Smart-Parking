using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Application.Common.Interfaces.Persistence;

public interface IReservationQuoteRepository
{
    Task<ReservationQuote?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(ReservationQuote quote, CancellationToken cancellationToken = default);
    Task<ReservationQuote?> FindReviewedAsync(Guid accountId, Guid id, CancellationToken ct);
    void Update(ReservationQuote quote);
}
