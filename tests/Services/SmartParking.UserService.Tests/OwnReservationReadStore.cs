using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.DTOs;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.UserService.Tests;

internal sealed class OwnReservationReadStore : IReservationBookingRepository
{
    public List<(Guid, ReservationRow)> Rows = []; public bool Fail;
    public Task<BookingReadResult?> FindOwnBookingAsync(Guid account, Guid id, CancellationToken ct) => Task.FromResult(Rows.Where(r => r.Item1 == account && r.Item2.Id == id).Select(r => new BookingReadResult(r.Item2, null, null, null)).SingleOrDefault());
    public Task<IReadOnlyList<BookingReadResult>> ListOwnBookingsAsync(Guid account, int take, DateTimeOffset? before, Guid? beforeId, CancellationToken ct, DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        if (Fail) throw new ReservationGateException(503, "DEPENDENCY_UNAVAILABLE", "Synthetic outage");
        IReadOnlyList<ReservationRow> rows = Rows.Where(r => r.Item1 == account).Select(r => r.Item2)
            .Where(r => from is null || r.ExpectedStartTime < to && r.ExpectedEndTime > from)
            .Where(r => before is null || r.CreatedAt < before || r.CreatedAt == before && r.Id.CompareTo(beforeId!.Value) < 0)
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Take(take).ToArray();
        return Task.FromResult<IReadOnlyList<BookingReadResult>>(rows.Select(r => new BookingReadResult(r, null, null, null)).ToArray());
    }
    public Task<ReservationRow?> FindBookingAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    public Task<string?> FindZeroDispositionReasonAsync(Guid id, Guid account, CancellationToken ct) => throw new NotSupportedException();
    public Task<ReservationRow?> FindOrderProvisioningAsync(Guid reservationId, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> HasQuoteBindingAsync(Guid quoteId, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> OrderAssignedToAnotherReservationAsync(Guid orderId, Guid reservationId, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<Guid>> DueOrderProvisioningAsync(DateTimeOffset now, int take, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<Guid>> DueZeroPriceAcceptanceAsync(DateTimeOffset now, int take, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<Guid>> DueNoPaymentConfirmationAsync(DateTimeOffset now, int take, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<Guid>> DuePaidConfirmationAsync(DateTimeOffset now, int take, CancellationToken ct) => throw new NotSupportedException();
    public Task<DateTimeOffset> DatabaseNowAsync(CancellationToken ct) => throw new NotSupportedException();
    public Task RequireDurableConfirmationAsync(CancellationToken ct) => throw new NotSupportedException();
    public Task RequirePlateAvailableAsync(ReservationRow row, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
    public Task<ReservationRow?> FindForUpdateAsync(Guid tenant, Guid site, Guid id, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<ReservationGateEntry>> ListAsync(Guid tenant, Guid site, CancellationToken ct) => throw new NotSupportedException();
    public Task<ReservationGateEntry?> FindAsync(Guid tenant, Guid site, string query, CancellationToken ct) => throw new NotSupportedException();
    public Task<ReservationGateEntry?> FindByIdAsync(Guid tenant, Guid site, Guid id, CancellationToken ct) => throw new NotSupportedException();
    public Task AddAsync(ReservationRow row, CancellationToken ct) => throw new NotSupportedException();
    public void Remove(ReservationRow row) => throw new NotSupportedException();
}
