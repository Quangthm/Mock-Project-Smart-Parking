using SmartParking.OperationService.Application.DTOs;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.FacilityService.Tests;

internal sealed class FakeBookingRepository(ReservationRow booking) : IReservationBookingRepository
{
    public Task AddAsync(ReservationRow entity, CancellationToken cancellationToken) => Task.CompletedTask;
    public void Remove(ReservationRow entity) { }
    public Task<ReservationRow?> FindForUpdateAsync(Guid tenantId, Guid siteId, Guid reservationId, CancellationToken cancellationToken) =>
        Task.FromResult<ReservationRow?>(booking.Id == reservationId ? booking : null);
    public Task<IReadOnlyList<ReservationGateEntry>> ListAsync(Guid tenantId, Guid siteId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ReservationGateEntry>>([]);
    public Task<ReservationGateEntry?> FindAsync(Guid tenantId, Guid siteId, string query, CancellationToken ct) =>
        Task.FromResult<ReservationGateEntry?>(null);
    public Task<ReservationGateEntry?> FindByIdAsync(Guid tenantId, Guid siteId, Guid reservationId, CancellationToken ct) =>
        Task.FromResult<ReservationGateEntry?>(null);
    public Task<BookingReadResult?> FindOwnBookingAsync(Guid accountId, Guid id, CancellationToken ct) =>
        Task.FromResult<BookingReadResult?>(null);
    public Task<IReadOnlyList<BookingReadResult>> ListOwnBookingsAsync(Guid accountId, int take, DateTimeOffset? before, Guid? beforeId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<BookingReadResult>>([]);
    public Task<ReservationRow?> FindOrderProvisioningAsync(Guid reservationId, CancellationToken ct) =>
        Task.FromResult<ReservationRow?>(booking.Id == reservationId ? booking : null);
    public Task<bool> OrderAssignedToAnotherReservationAsync(Guid orderId, Guid reservationId, CancellationToken ct) => Task.FromResult(false);
    public Task<IReadOnlyList<Guid>> DueOrderProvisioningAsync(DateTimeOffset now, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<Guid>>([]);
    public Task<IReadOnlyList<Guid>> DueHoldExpiryAsync(DateTimeOffset now, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<Guid>>([]);
    public Task<IReadOnlyList<Guid>> DueZeroPriceAcceptanceAsync(DateTimeOffset now, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<Guid>>([]);
    public Task<IReadOnlyList<Guid>> DueNoPaymentConfirmationAsync(DateTimeOffset now, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<Guid>>([]);
    public Task<IReadOnlyList<Guid>> DuePaidConfirmationAsync(DateTimeOffset now, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<Guid>>([]);
    public Task<DateTimeOffset> DatabaseNowAsync(CancellationToken ct) => Task.FromResult(DateTimeOffset.UtcNow);
    public Task RequireDurableConfirmationAsync(CancellationToken ct) => Task.CompletedTask;
    public Task RequirePlateAvailableAsync(ReservationRow proposed, DateTimeOffset now, CancellationToken ct) => Task.CompletedTask;
}
