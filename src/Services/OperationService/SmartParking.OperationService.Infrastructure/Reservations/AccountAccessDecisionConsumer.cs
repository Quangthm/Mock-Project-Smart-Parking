using MassTransit;
using SmartParking.Contracts.AccountAccess;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class AccountAccessDecisionConsumer(IReservationRequestRepository requests)
    : IConsumer<AccountAccessDecisionV1>
{
    public async Task Consume(ConsumeContext<AccountAccessDecisionV1> context)
    {
        var decision = context.Message;
        if (context.CorrelationId != decision.WorkflowId || context.MessageId != decision.AttemptId)
            throw new ArgumentException("Invalid Account decision envelope.");
        var request = await requests.FindForWorkerAsync(decision.WorkflowId, context.CancellationToken);
        if (request?.AccountCheck is null) return;
        var saga = await context.GetSendEndpoint(new Uri($"queue:{AdmissionMessages.SagaQueue}"));
        await saga.Send(decision, send => send.CorrelationId = decision.WorkflowId, context.CancellationToken);
    }
}
