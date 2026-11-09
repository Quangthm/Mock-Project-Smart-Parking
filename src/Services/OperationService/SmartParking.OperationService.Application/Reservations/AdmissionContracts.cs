using MediatR;
using SmartParking.Contracts.AccountAccess;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public interface ICurrentActor
{
    Guid? UserId { get; }
    Guid? AccountId { get; }
    string? Role { get; }
}

public interface IReservationRequestRepository
{
    Task LockAdmissionAsync(Guid accountId, Guid key, CancellationToken ct);
    Task<ReservationRequest?> FindByKeyAsync(Guid accountId, Guid key, CancellationToken ct);
    Task<ReservationRequest?> FindOwnAsync(Guid accountId, Guid id, CancellationToken ct);
    Task<ReservationRequest?> LockAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Guid>> DueAsync(DateTimeOffset now, CancellationToken ct);
    void Add(ReservationRequest request);
}

public interface IAccountAccessRequester
{
    Task RequestAsync(AccountAccessRequestV1 request, CancellationToken ct);
}

public sealed record CreateReservationRequest(Guid VehicleId, Guid SiteId, Guid RequestedSlotId,
    string Mode, DateTimeOffset Start, DateTimeOffset End, Guid TermsId, string TermsVersion);

public sealed record AdmitReservationCommand(CreateReservationRequest Intent, Guid IdempotencyKey) : IRequest<ReservationReceipt>;
public sealed record ReadReservationReceiptQuery(Guid RequestId) : IRequest<ReservationReceipt>;
public sealed record AdvanceReservationAuthorizationCommand(Guid RequestId) : IRequest;
public sealed record RecordAccountAccessDecisionCommand(AccountAccessDecisionV1 Decision) : IRequest;
public sealed record ReservationReceipt(Guid RequestId, string State, Guid? ReservationId, string? Code);
