using MassTransit;
using MediatR;
using SmartParking.Contracts.ReservationAdmission;
using SmartParking.FacilityService.Application.Reservations;
using SmartParking.FacilityService.Domain;

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
            var facts = await mediator.Send(new ResolveReservationSlotQuery(input.RequestedSlotId), context.CancellationToken);
            if (facts.SlotId != input.RequestedSlotId || facts.SiteId == Guid.Empty || facts.TenantId == Guid.Empty ||
                string.IsNullOrWhiteSpace(facts.Version) || !facts.ParticipatesInStructureGuard)
                decision = new(input, "DEPENDENCY_UNAVAILABLE", null, null, null);
            else if (!facts.EligibleCarSlot) decision = new(input, "INELIGIBLE", null, null, null);
            else decision = new(input, "ELIGIBLE", facts.SiteId, facts.TenantId, facts.Version);
        }
        catch (StructureException error) when (error.Code is "SLOT_NOT_FOUND" or "SITE_NOT_FOUND")
        {
            decision = new(input, "INELIGIBLE", null, null, null);
        }
        catch (StructureException error) when (error.Code == "STRUCTURE_BUSY")
        {
            decision = new(input, "DEPENDENCY_UNAVAILABLE", null, null, null);
        }
        var replies = await context.GetSendEndpoint(new Uri("queue:operations-reservation-slot-results-v1"));
        await replies.Send(decision, send => send.CorrelationId = input.WorkflowId, context.CancellationToken);
    }
}
