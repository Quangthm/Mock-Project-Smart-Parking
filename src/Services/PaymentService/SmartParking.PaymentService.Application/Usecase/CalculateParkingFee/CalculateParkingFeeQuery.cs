using MediatR;
using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.Application.Usecase.CalculateParkingFee;

public sealed record CalculateParkingFeeQuery(
    Guid TenantId,
    Guid SiteId,
    string VehicleType,
    DateTimeOffset EntryTime,
    DateTimeOffset ExitTime,
    Guid? SessionId = null,
    Guid? ReservationId = null,
    PricingPolicySnapshot? PolicyOverride = null,
    bool IsTrustedInternalCaller = false
) : IRequest<PricingCalculationResult>;
