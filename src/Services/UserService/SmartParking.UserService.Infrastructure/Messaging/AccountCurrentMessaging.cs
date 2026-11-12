using MassTransit;
using MediatR;
using SmartParking.Contracts.AccountAccess;
using UserService.Application.Authorization;

namespace UserService.Infrastructure.Messaging;

public sealed class AccountCurrentConsumer(IMediator mediator) : IConsumer<AccountAccessRequestV1>
{
    public Task Consume(ConsumeContext<AccountAccessRequestV1> context) =>
        mediator.Send(new EvaluateAccountCurrentCommand(context.Message), context.CancellationToken);
}

public sealed class AccountAccessDecisionPublisher(ISendEndpointProvider endpoints) : IAccountAccessDecisionPublisher
{
    public async Task SendAsync(AccountAccessDecisionV1 decision, CancellationToken ct)
    {
        var endpoint = await endpoints.GetSendEndpoint(new Uri($"queue:{AccountAccessEndpoints.Decisions}"));
        await endpoint.Send(decision, context =>
        {
            // One business decision, distinct response envelopes for different delivery attempts.
            // Using DecisionId here would make the Operations inbox discard a later timely replay.
            context.MessageId = decision.AttemptId;
            context.CorrelationId = decision.WorkflowId;
        }, ct);
    }
}
