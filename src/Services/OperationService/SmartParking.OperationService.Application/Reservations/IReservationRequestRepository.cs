using MediatR;
using SmartParking.Contracts.AccountAccess;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public interface IReservationRequestRepository
{
    Task LockAdmissionAsync(Guid accountId, Guid key, CancellationToken ct);
    Task<ReservationRequest?> FindByKeyAsync(Guid accountId, Guid key, CancellationToken ct);
    Task<ReservationRequest?> FindOwnAsync(Guid accountId, Guid id, CancellationToken ct);
    Task<ReservationRequest?> FindForWorkerAsync(Guid id, CancellationToken ct);
    Task<ReservationRequest?> LockAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Guid>> DueAsync(DateTimeOffset now, bool includeSaga, CancellationToken ct);
    Task<Guid?> FindOwnByOrderAsync(Guid accountId, Guid orderId, CancellationToken ct);
    void Add(ReservationRequest request);
}
