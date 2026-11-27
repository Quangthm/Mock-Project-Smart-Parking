using MassTransit;
using MediatR;
using SmartParking.Contracts.ReservationAdmission;
using UserService.Application.Authorization;

namespace UserService.Infrastructure.Messaging;

public sealed class ReservationPlateConsumer(IMediator mediator) : IConsumer<PlateCheckV1>
{
    public async Task Consume(ConsumeContext<PlateCheckV1> context)
    {
        if (context.CorrelationId != context.Message.WorkflowId) throw new ArgumentException("Invalid correlation.");
        var decision = await mediator.Send(new ResolveEligibleRegisteredPlate(context.Message), context.CancellationToken);
        var replies = await context.GetSendEndpoint(new Uri("queue:operations-reservation-plate-results-v1"));
        await replies.Send(decision, send => send.CorrelationId = context.CorrelationId, context.CancellationToken);
    }
}
