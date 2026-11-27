using MassTransit;
using SmartParking.Contracts.ReservationAdmission;

namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class PlateDecisionConsumer : IConsumer<PlateDecisionV1>
{
    public async Task Consume(ConsumeContext<PlateDecisionV1> context)
    {
        var id = context.Message.Request.WorkflowId;
        if (id == Guid.Empty || context.CorrelationId != id) throw new ArgumentException("Invalid correlation.");
        var saga = await context.GetSendEndpoint(new Uri($"queue:{AdmissionMessages.SagaQueue}"));
        await saga.Send(context.Message, send => send.CorrelationId = id, context.CancellationToken);
    }
}
