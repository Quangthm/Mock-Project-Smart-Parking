using MassTransit;
using MediatR;
using SmartParking.OperationService.Application.Usecase.ReservationGate;

namespace SmartParking.OperationService.Infrastructure.Messaging;

[MessageUrn("SmartParking.PaymentService.Domain.Events:PaymentSucceededV1")]
public sealed record PaymentSucceededV1
{
    public Guid EventId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public Guid PaymentOrderId { get; init; }
    public Guid PaymentTransactionId { get; init; }
    public string PaymentMethod { get; init; } = "PAYOS";
    public string? GatewayReference { get; init; }
    public Guid TenantId { get; init; }
    public Guid SiteId { get; init; }
    public string ReferenceType { get; init; } = "PARKING_SESSION";
    public Guid ReferenceId { get; init; }
    public string? PlateNumber { get; init; }
    public Guid? AccountId { get; init; }
    public decimal Amount { get; init; }
    public decimal ParkingFee { get; init; }
    public decimal? PenaltyFee { get; init; }
    public string? PenaltyReason { get; init; }
    public string Currency { get; init; } = "VND";
}

public sealed class PaymentSucceededConsumer(IMediator mediator) : IConsumer<PaymentSucceededV1>
{
    public Task Consume(ConsumeContext<PaymentSucceededV1> context)
    {
        var payment = context.Message;
        return mediator.Send(new SimulatePaidGateDepartureCommand(
            payment.TenantId, payment.SiteId, payment.ReferenceType, payment.ReferenceId,
            payment.PaymentOrderId, payment.OccurredAt), context.CancellationToken);
    }
}
