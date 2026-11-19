using MediatR;
using SmartParking.Contracts.AccountAccess;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record CreateReservationRequest(Guid VehicleId, Guid SiteId, Guid RequestedSlotId,
    string Mode, DateTimeOffset Start, DateTimeOffset End, Guid TermsId, string TermsVersion, string PaymentMethod = "");
