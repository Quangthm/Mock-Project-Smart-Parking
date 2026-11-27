using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Reservations;

// A specialized port on the SAME ReservationRepository/aggregate, not a second persistence store.
public interface IReservationBookingRepository : IReservationRepository
{
    Task<BookingReadResult?> FindOwnBookingAsync(Guid accountId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<BookingReadResult>> ListOwnBookingsAsync(Guid accountId, int take,
        DateTimeOffset? before, Guid? beforeId, CancellationToken ct, DateTimeOffset? from = null, DateTimeOffset? to = null);
    Task<ReservationRow?> FindBookingAsync(Guid reservationId, CancellationToken ct);
    Task<string?> FindZeroDispositionReasonAsync(Guid reservationId, Guid accountId, CancellationToken ct);
    Task<bool> HasQuoteBindingAsync(Guid quoteId, CancellationToken ct);
    Task<bool> OrderAssignedToAnotherReservationAsync(Guid orderId, Guid reservationId, CancellationToken ct);
    Task<DateTimeOffset> DatabaseNowAsync(CancellationToken ct);
    Task RequireDurableConfirmationAsync(CancellationToken ct);
    Task RequirePlateAvailableAsync(ReservationRow proposed, DateTimeOffset now, CancellationToken ct);
}
