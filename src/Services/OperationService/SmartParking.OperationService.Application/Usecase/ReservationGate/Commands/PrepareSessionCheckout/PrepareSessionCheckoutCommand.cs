using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Services;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed record PrepareSessionCheckoutCommand(
    Guid SiteId,
    Guid SessionId,
    string? PlateNumber) : IRequest<CheckoutPaymentPreparation>;