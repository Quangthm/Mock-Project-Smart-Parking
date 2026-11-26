using MassTransit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class QuoteEvaluationConsumer(IServiceScopeFactory scopes) : IConsumer<EvaluateQuoteBooking>
{
    public async Task Consume(ConsumeContext<EvaluateQuoteBooking> context)
    {
        var id = context.Message.RequestId;
        if (id == Guid.Empty || context.CorrelationId != id) throw new ArgumentException("Invalid evaluation correlation.");
        // Isolate the database bus outbox from the receive context. The read-only Payment call precedes
        // any transaction. The request row fences redelivery after the local transaction commits.
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new CommitQuoteHoldCommand(id), context.CancellationToken);
    }
}
