using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed class CalculateCheckoutPricingQueryHandler(
    IOperatorIdentityClient identity,
    ICheckoutPaymentClient payment) : IRequestHandler<CalculateCheckoutPricingQuery, CheckoutPricing>
{
    public async Task<CheckoutPricing> Handle(CalculateCheckoutPricingQuery request, CancellationToken ct)
    {
        if (request.ReferenceType is not ("RESERVATION" or "PARKING_SESSION"))
            throw new ReservationGateException(400, "INVALID_REFERENCE_TYPE", "Checkout reference type is invalid.");
        if (request.EntryTime > request.ExitTime)
            throw new ReservationGateException(400, "INVALID_TIME_RANGE", "Checkout time range is invalid.");

        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        return await payment.CalculateAsync(
            scope.TenantId,
            request.SiteId,
            request.ReferenceId,
            request.ReferenceType,
            request.VehicleType,
            request.EntryTime,
            request.ExitTime,
            ct);
    }
}