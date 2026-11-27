using System.Data.Common;
using FluentValidation;
using MassTransit;
using MediatR;
using SmartParking.Contracts.ReservationAdmission;
using SmartParking.FacilityService.Application.Reservations;

namespace SmartParking.FacilityService.Infrastructure;

public sealed class ReservationSlotConsumer(IMediator mediator) : IConsumer<SlotCheckV1>
{
    public async Task Consume(ConsumeContext<SlotCheckV1> context)
    {
        var input = context.Message;
        if (context.CorrelationId != input.WorkflowId || input.WorkflowId == Guid.Empty || input.AccountId == Guid.Empty ||
            input.RequestedSlotId == Guid.Empty || input.AttemptId == Guid.Empty || string.IsNullOrWhiteSpace(input.Fingerprint))
            throw new ArgumentException("Invalid slot check.");
        SlotDecisionV1 decision;
        try
        {
            var facts = await mediator.Send(new CheckReservationSlotEligibilityQuery(
                input.RequestedSlotId, input.Start, input.End), context.CancellationToken);
            decision = facts switch
            {
                { Outcome: "ELIGIBLE", SiteId: Guid siteId, TenantId: Guid tenantId }
                    when facts.SlotId == input.RequestedSlotId && siteId != Guid.Empty && tenantId != Guid.Empty =>
                    new(input, "ELIGIBLE", siteId, tenantId),
                { Outcome: "INELIGIBLE", SiteId: null, TenantId: null } when facts.SlotId == input.RequestedSlotId =>
                    new(input, "INELIGIBLE", null, null),
                _ => new(input, "DEPENDENCY_UNAVAILABLE", null, null)
            };
        }
        catch (Exception error) when (error is ValidationException or DbException or TimeoutException)
        {
            decision = new(input, "DEPENDENCY_UNAVAILABLE", null, null);
        }
        var replies = await context.GetSendEndpoint(new Uri("queue:operations-reservation-slot-results-v1"));
        await replies.Send(decision, send => send.CorrelationId = input.WorkflowId, context.CancellationToken);
    }
}
