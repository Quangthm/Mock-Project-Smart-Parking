using MediatR;
using SmartParking.PaymentService.Application.DTOs.Invoices;

namespace SmartParking.PaymentService.Application.Usecase.PrepareParkingFeeOrder;

public sealed record PrepareParkingFeeOrderCommand(
    Guid TenantId,
    Guid SiteId,
    Guid SessionId,
    string VehicleType,
    string PlateNumber,
    DateTimeOffset EntryTime,
    DateTimeOffset PricingCutoffAt,
    Guid? AccountId,
    decimal? PenaltyAmount,
    string? PenaltyReason,
    string IdempotencyKey
) : IRequest<PrepareParkingFeeOrderResponseDto>;
