using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Reservations;

// A specialized port on the SAME ReservationRepository/aggregate, not a second persistence store.
public interface IReservationBookingRepository : IReservationRepository
{
    Task<BookingReadResult?> FindOwnBookingAsync(Guid accountId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<BookingReadResult>> ListOwnBookingsAsync(Guid accountId, int take,
        DateTimeOffset? before, Guid? beforeId, CancellationToken ct);
    Task<ReservationRow?> FindOrderProvisioningAsync(Guid reservationId, CancellationToken ct);
    Task<bool> OrderAssignedToAnotherReservationAsync(Guid orderId, Guid reservationId, CancellationToken ct);
    Task<IReadOnlyList<Guid>> DueOrderProvisioningAsync(DateTimeOffset now, int take, CancellationToken ct);
    Task<IReadOnlyList<Guid>> DueHoldExpiryAsync(DateTimeOffset now, int take, CancellationToken ct);
    Task<IReadOnlyList<Guid>> DueZeroPriceAcceptanceAsync(DateTimeOffset now, int take, CancellationToken ct);
    Task<IReadOnlyList<Guid>> DueNoPaymentConfirmationAsync(DateTimeOffset now, int take, CancellationToken ct);
    Task<IReadOnlyList<Guid>> DuePaidConfirmationAsync(DateTimeOffset now, int take, CancellationToken ct);
    Task<DateTimeOffset> DatabaseNowAsync(CancellationToken ct);
    Task RequireDurableConfirmationAsync(CancellationToken ct);
    Task RequirePlateAvailableAsync(ReservationRow proposed, DateTimeOffset now, CancellationToken ct);
}
