using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Services;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed record PrepareReservationCheckoutCommand(
    Guid SiteId,
    Guid ReservationId,
    string? PlateNumber) : IRequest<CheckoutPaymentPreparation>;