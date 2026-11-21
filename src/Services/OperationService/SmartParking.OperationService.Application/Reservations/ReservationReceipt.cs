using MediatR;
using SmartParking.Contracts.AccountAccess;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record ReservationReceipt(Guid RequestId, string State, Guid? ReservationId, string? Code,
    int ContractVersion = 2);
