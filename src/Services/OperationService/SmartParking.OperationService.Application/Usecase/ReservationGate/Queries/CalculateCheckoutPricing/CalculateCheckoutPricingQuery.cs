using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Services;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed record CalculateCheckoutPricingQuery(
    Guid SiteId,
    Guid ReferenceId,
    string ReferenceType,
    string VehicleType,
    DateTimeOffset EntryTime,
    DateTimeOffset ExitTime) : IRequest<CheckoutPricing>;