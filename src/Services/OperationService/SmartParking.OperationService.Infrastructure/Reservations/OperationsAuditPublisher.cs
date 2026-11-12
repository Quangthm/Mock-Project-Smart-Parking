using MassTransit;
using SmartParking.Contracts.Audit;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;

namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class OperationsAuditPublisher(IPublishEndpoint publisher) : IOperationsAuditPublisher
{
    public Task PublishAsync(OperationsGateAuditV1 auditEvent, CancellationToken ct) =>
        publisher.Publish(auditEvent, context =>
        {
            context.MessageId = auditEvent.EventId;
            context.CorrelationId = auditEvent.ReservationId ?? auditEvent.ParkingSessionId;
        }, ct);
}

public sealed class UnconfiguredOperationsAuditPublisher : IOperationsAuditPublisher
{
    public Task PublishAsync(OperationsGateAuditV1 auditEvent, CancellationToken ct) =>
        throw new ReservationGateException(503, "AUDIT_PUBLISHING_UNAVAILABLE", "Durable audit publishing is not configured.");
}
