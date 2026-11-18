using MediatR;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Reservations;

public sealed class ResolveReservationSlotHandler(IReservationDependencyRepository repository, ISender sender)
    : IRequestHandler<ResolveReservationSlotQuery, BookingFacts>
{
    public async Task<BookingFacts> Handle(ResolveReservationSlotQuery query, CancellationToken ct)
    {
        var siteId = await repository.FindSlotSiteAsync(query.SlotId, ct)
            ?? throw new StructureException("SLOT_NOT_FOUND", "The selected slot was not found.");
        return await sender.Send(new ReadBookingFactsQuery(siteId, query.SlotId), ct);
    }
}
