using MediatR;
using SmartParking.PaymentService.Application.DTOs.Payments;
using SmartParking.PaymentService.Application.Usecase.CalculateParkingFee;
using SmartParking.PaymentService.Application.Usecase.CreatePaymentOrder;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;

namespace SmartParking.PaymentService.Application.Usecase.PreparePaymentOrder;

public sealed record PreparePaymentOrderRequestDto(
    Guid TenantId,
    Guid SiteId,
    Guid SessionId,
    string VehicleType,
    DateTimeOffset EntryTime,
    DateTimeOffset PricingCutoffAt,
    string PlateNumber,
    Guid? AccountId = null,
    decimal PenaltyAmount = 0,
    string? PenaltyReason = null,
    string? IdempotencyKey = null,
    string ReferenceType = "PARKING_SESSION",
    Guid? ReferenceId = null);

public sealed record PreparePaymentOrderCommand(PreparePaymentOrderRequestDto Request)
    : IRequest<PreparePaymentOrderResponseDto>;

public sealed record PreparePaymentOrderResponseDto(
    Guid OrderId,
    string OrderCode,
    decimal ParkingFee,
    decimal PenaltyFee,
    decimal FinalAmount,
    string Currency,
    string Status,
    DateTimeOffset ExpiresAt,
    decimal TotalParkedMinutes);

public sealed class PreparePaymentOrderCommandHandler(IMediator mediator)
    : IRequestHandler<PreparePaymentOrderCommand, PreparePaymentOrderResponseDto>
{
    public async Task<PreparePaymentOrderResponseDto> Handle(
        PreparePaymentOrderCommand command,
        CancellationToken ct)
    {
        var request = command.Request;
        if (request.PenaltyAmount < 0)
            throw new PaymentDomainException("INVALID_PENALTY", "Penalty amount cannot be negative.");
        if (string.IsNullOrWhiteSpace(request.PlateNumber))
            throw new PaymentDomainException("INVALID_PLATE", "Plate number is required.");
        if (request.EntryTime > request.PricingCutoffAt)
            throw new PaymentDomainException("INVALID_TIME_RANGE", "Pricing cutoff must be after entry time.");

        if (!Enum.TryParse<ReferenceType>(request.ReferenceType, true, out var referenceType) ||
            referenceType is not (ReferenceType.PARKING_SESSION or ReferenceType.RESERVATION))
            throw new PaymentDomainException("INVALID_REFERENCE_TYPE", "Checkout reference type is invalid.");
        var referenceId = request.ReferenceId ?? request.SessionId;
        var pricing = await mediator.Send(new CalculateParkingFeeQuery(
            request.TenantId,
            request.SiteId,
            request.VehicleType,
            request.EntryTime,
            request.PricingCutoffAt,
            SessionId: referenceType == ReferenceType.PARKING_SESSION ? referenceId : null,
            ReservationId: referenceType == ReferenceType.RESERVATION ? referenceId : null,
            IsTrustedInternalCaller: true), ct);

        var order = await mediator.Send(new CreatePaymentOrderCommand(
            new CreatePaymentOrderRequestDto(
                request.TenantId,
                request.SiteId,
                referenceId,
                referenceType,
                pricing.FinalAmountVnd + request.PenaltyAmount,
                request.AccountId,
                request.IdempotencyKey,
                request.PlateNumber)), ct);

        return new(
            order.OrderId,
            order.OrderCode,
            pricing.FinalAmountVnd,
            request.PenaltyAmount,
            order.Amount,
            "VND",
            order.Status,
            order.ExpiresAt,
            pricing.TotalParkedMinutes);
    }
}
