using Microsoft.Extensions.DependencyInjection.Extensions;
using MassTransit;
using SmartParking.Contracts.Audit;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartParking.Contracts.AccountAccess;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Infrastructure.Messaging;

namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class AccountAccessDecisionConsumer(IMediator mediator, IReservationRequestRepository requests)
    : IConsumer<AccountAccessDecisionV1>
{
    public async Task Consume(ConsumeContext<AccountAccessDecisionV1> context)
    {
        var decision = context.Message;
        if (context.CorrelationId != decision.WorkflowId || context.MessageId != decision.AttemptId)
            throw new ArgumentException("Invalid Account decision envelope.");
        var request = await requests.FindForWorkerAsync(decision.WorkflowId, context.CancellationToken);
        if (request?.AccountCheck is null)
            await mediator.Send(new RecordAccountAccessDecisionCommand(decision), context.CancellationToken);
        else
        {
            var saga = await context.GetSendEndpoint(new Uri($"queue:{AdmissionMessages.SagaQueue}"));
            await saga.Send(decision, send => send.CorrelationId = decision.WorkflowId, context.CancellationToken);
        }
    }
}
